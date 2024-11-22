using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Wrappers;
using BYAMLSharp;
using NARCSharp;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;

namespace Autumn.FileSystems;

internal partial class RomFSHandler
{
    [GeneratedRegex("(.*)(Design|Map|Sound)(\\d+\\b).szs")]
    private static partial Regex StageFileRegex();

    private static readonly Regex _stagesRegex = StageFileRegex();

    private static readonly Encoding s_byamlEncoding = Encoding.GetEncoding("Shift-JIS");

    public string Root { get; }

    private readonly string _stagesPath;
    private readonly string _actorsPath;
    private readonly string _ccntPath;

    private readonly Dictionary<string, Actor> _cachedActors = new();
    private readonly Dictionary<string, DateTime> _cachedActorsTimestamps = new();

    private ReadOnlyDictionary<string, string>? _creatorClassNameTable = null;

    public RomFSHandler(string path)
    {
        Root = path;

        _stagesPath = Path.Join(Root, "StageData");
        _actorsPath = Path.Join(Root, "ObjectData");
        _ccntPath = Path.Join(Root, "SystemData", "CreatorClassNameTable.szs");
    }

    public bool ExistsStage(string name, byte scenario)
    {
        string[] paths =
        [
            Path.Join(_stagesPath, $"{name}Design{scenario}.szs"),
            Path.Join(_stagesPath, $"{name}Map{scenario}.szs"),
            Path.Join(_stagesPath, $"{name}Sound{scenario}.szs")
        ];

        foreach (string path in paths)
        {
            if (File.Exists(path))
                return true;
        }

        return false;
    }

    public bool ExistsActor(string name)
    {
        string path = Path.Join(_actorsPath, name + ".szs");
        return File.Exists(path);
    }

    public bool ExistsCreatorClassNameTable() => File.Exists(_ccntPath);

    public IEnumerable<(string Name, byte Scenario)> EnumerateStages()
    {
        if (!Directory.Exists(_stagesPath))
            yield break;

        HashSet<(string, byte)> stages = new();

        foreach (string file in Directory.EnumerateFiles(_stagesPath))
        {
            string fileName = Path.GetFileName(file);

            Match match = _stagesRegex.Match(fileName);

            if (!match.Success)
                continue;

            string name = match.Groups[1].Value;
            byte scenario = byte.Parse(match.Groups[3].Value);

            var stage = (name, scenario);

            if (stages.Add(stage))
                yield return stage;
        }
    }

    public Stage ReadStage(string name, byte scenario)
    {
        Stage stage = new(initialize: false) { Name = name, Scenario = scenario };

        (string, StageFileType)[] paths =
        [
            (Path.Join(_stagesPath, $"{name}Design{scenario}.szs"), StageFileType.Design),
            (Path.Join(_stagesPath, $"{name}Map{scenario}.szs"), StageFileType.Map),
            (Path.Join(_stagesPath, $"{name}Sound{scenario}.szs"), StageFileType.Sound)
        ];

        foreach (var (path, fileType) in paths)
        {
            if (!File.Exists(path))
                continue;

            NARCFileSystem? narc = SZSWrapper.ReadFile(path);

            if (narc is null)
                continue;

            foreach (var (filename, contents) in narc.EnumerateFiles())
            {
                // Temporary code for reading any files that are not StageData.
                if (filename != "StageData.byml")
                {
                    stage.AddAdditionalFile(fileType, filename, contents);
                    continue;
                }

                // Read StageObjs from current stage file:
                BYAML stageDataByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                IEnumerable<StageObj> stageObjs = ProcessStageObjs(stageDataByaml, fileType);

                stage.AddStageObjs(stageObjs);
            }
        }

        return stage;
    }

    public bool WriteStage(Stage stage)
    {
        uint currentId = 0;

        return false; // TODO
    }

    public Actor ReadActor(string name, GL gl)
    {
        string path = Path.Join(_actorsPath, name + ".szs");

        // Return cached actor if valid (not modified externally)
        if (_cachedActors.TryGetValue(path, out Actor? cachedActor))
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            if (
                _cachedActorsTimestamps.TryGetValue(path, out DateTime oldTimestamp)
                && oldTimestamp == timestamp
            )
                return cachedActor;
        }

        Actor actor = new(name, gl);

        // The actor will result in an empty model if the narc is null (not found or
        // not a narc) or if the narc does not contain the properly-named cgfx file.

        if (!File.Exists(path))
            return actor;

        NARCFileSystem? narc = SZSWrapper.ReadFile(path);

        if (narc is null)
            return actor;

        bool found = narc.TryGetFile(name + ".bcmdl", out byte[] cgfx);

        if (!found)
            return actor;

        H3D h3D;

        try
        {
            using MemoryStream stream = new(cgfx);
            h3D = Gfx.OpenAsH3D(stream);
        }
        catch
        {
            Debug.Write($"The actor's cgfx could not be read ({name})", "Error");
            return actor;
        }

        foreach (H3DTexture texture in h3D.Textures)
        {
            actor.AddTexture(texture);
        }

        foreach (H3DLUT lut in h3D.LUTs)
        foreach (H3DLUTSampler sampler in lut.Samplers)
        {
            actor.AddLUTTexture(lut.Name, sampler);
        }

        foreach (H3DModel model in h3D.Models)
        {
            List<H3DMesh>[] meshLists =
            [
                model.MeshesLayer0, // Opaque layer
                model.MeshesLayer1, // Translucent layer
                model.MeshesLayer2, // Substractive layer
                model.MeshesLayer3 // Additive layer
            ];

            for (int i = 0; i < meshLists.Length; i++)
            {
                foreach (H3DMesh mesh in meshLists[i])
                {
                    // Obtain the mesh's material by its index.
                    int matIdx = mesh.MaterialIndex;
                    H3DMaterial material = model.Materials[matIdx];

                    // Obtain submesh culling.
                    int meshIdx = model.Meshes.IndexOf(mesh);

                    H3DSubMeshCulling? subMeshCulling = null;

                    if (model.SubMeshCullings.Count > meshIdx)
                        subMeshCulling = model.SubMeshCullings[meshIdx];

                    var skeleton = model.Skeleton;

                    actor.AddMesh((H3DMeshLayer)i, mesh, subMeshCulling, material, skeleton);
                }
            }
        }

        // Cache the actor
        _cachedActors.Add(path, actor);
        _cachedActorsTimestamps.Add(path, File.GetLastWriteTime(path));

        return actor;
    }

    public ReadOnlyDictionary<string, string> ReadCreatorClassNameTable()
    {
        if (_creatorClassNameTable is null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(_ccntPath);

            Dictionary<string, string> dict = new();

            do
            {
                if (narc is null)
                    break;

                byte[] tableData = narc.GetFile("CreatorClassNameTable.byml");

                if (tableData.Length == 0)
                    break;

                BYAML byaml = BYAMLParser.Read(tableData, s_byamlEncoding);

                if (byaml.RootNode.NodeType != BYAMLNodeType.Array)
                    break;

                BYAMLNode[] nodes = byaml.RootNode.GetValueAs<BYAMLNode[]>()!;

                foreach (BYAMLNode node in nodes)
                {
                    if (node.NodeType != BYAMLNodeType.Dictionary)
                        continue;

                    var entry = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                    if (
                        !entry.TryGetValue("ObjectName", out BYAMLNode? objectName)
                        || !entry.TryGetValue("ClassName", out BYAMLNode? className)
                        || objectName.NodeType != BYAMLNodeType.String
                        || className.NodeType != BYAMLNodeType.String
                    )
                        continue;

                    dict.Add(objectName.GetValueAs<string>()!, className.GetValueAs<string>()!);
                }
            } while (false);

            _creatorClassNameTable = new(dict);
        }

        return _creatorClassNameTable;
    }

    private static IEnumerable<StageObj> ProcessStageObjs(BYAML byaml, StageFileType fileType)
    {
        if (byaml.RootNode.NodeType is not BYAMLNodeType.Dictionary)
            throw new("The given BYAML was not formatted correctly.");

        var rootDict = byaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        // Rails:

        Dictionary<BYAMLNode, RailObj> processedRails = new();

        BYAMLNode allRailInfosNode = rootDict["AllRailInfos"];
        var allRailInfos = allRailInfosNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        Debug.Assert(allRailInfos.Count <= 1);

        allRailInfos.TryGetValue("RailInfo", out BYAMLNode? railInfoNode);
        var railInfos = railInfoNode?.GetValueAs<BYAMLNode[]>() ?? Array.Empty<BYAMLNode>();

        foreach (BYAMLNode node in railInfos)
        {
            if (node.NodeType != BYAMLNodeType.Dictionary)
                continue;

            RailObj railObj = ReadRailObj(node, fileType);

            processedRails.Add(node, railObj);

            yield return railObj;
        }

        // All others:

        BYAMLNode allInfosNode = rootDict["AllInfos"];
        var allInfos = allInfosNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        foreach (KeyValuePair<string, BYAMLNode> info in allInfos)
        {
            if (info.Value.NodeType != BYAMLNodeType.Array)
                continue;

            BYAMLNode[] array = info.Value.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode node in array)
            {
                if (node.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObjType objType = info.Key switch
                {
                    "AreaObjInfo" => StageObjType.Area,
                    "CameraAreaInfo" => StageObjType.CameraArea,
                    "GoalObjInfo" => StageObjType.Goal,
                    "ObjInfo" => StageObjType.Regular,
                    "StartEventObjInfo" => StageObjType.StartEvent,
                    "StartInfo" => StageObjType.Start,
                    "DemoSceneObjInfo" => StageObjType.DemoScene,
                    _ => throw new NotSupportedException("Unknown stage obj type found.")
                };

                StageObj stageObj = ReadStageObj(node, processedRails, objType, fileType);

                yield return stageObj;
            }
        }
    }

    private static RailObj ReadRailObj(BYAMLNode node, StageFileType fileType)
    {
        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        dict.TryGetValue("l_id", out BYAMLNode? id);
        dict.TryGetValue("name", out BYAMLNode? name);
        dict.TryGetValue("LayerName", out BYAMLNode? layerName);
        dict.TryGetValue("no", out BYAMLNode? railNo);
        dict.TryGetValue("closed", out BYAMLNode? railClosed);
        dict.TryGetValue("type", out BYAMLNode? railType);

        dict.TryGetValue("num_pnt", out BYAMLNode? pointCount);
        dict.TryGetValue("Points", out BYAMLNode? railPointsNode);

        RailPointType pointType = railType?.GetValueAs<string>() switch
        {
            "Bezier" => RailPointType.Bezier,
            "Linear" => RailPointType.Linear,
            _ => throw new NotImplementedException("Unknown rail point type.")
        };

        Debug.Assert(pointType == RailPointType.Bezier);

        RailObj railObj =
            new()
            {
                PointType = pointType,
                Type = StageObjType.Rail,
                FileType = fileType,
                Name = name?.GetValueAs<string>() ?? "RailStageObj",
                Layer = layerName?.GetValueAs<string>() ?? "共通",
                RailNo = railNo?.GetValueAs<int>() ?? 0,
                Closed = railClosed?.GetValueAs<string>() == "CLOSE",
                Properties = dict.Where(i =>
                        i.Key != "no"
                        && i.Key != "closed"
                        && i.Key != "type"
                        && i.Key != "num_pnt"
                        && i.Key != "Points"
                        && i.Key != "name"
                        && i.Key != "LayerName"
                        && i.Key != "l_id"
                    )
                    .ToDictionary(i => i.Key, i => i.Value.Value)
            };

        // Rail point reading:

        BYAMLNode[] railPointNodes =
            railPointsNode?.GetValueAs<BYAMLNode[]>() ?? Array.Empty<BYAMLNode>();

        Debug.Assert(pointCount?.GetValueAs<int>() == railPointNodes.Length);

        for (int i = 0; i < railPointNodes.Length; i++)
        {
            BYAMLNode railPointNode = railPointNodes[i];

            if (railPointNode?.NodeType != BYAMLNodeType.Dictionary)
                continue;

            var railPointDict = railPointNode?.GetValueAs<Dictionary<string, BYAMLNode>>()!;

            railPointDict.TryGetValue("id", out BYAMLNode? pointID);

            railPointDict.TryGetValue("pnt0_x", out BYAMLNode? pnt0X);
            railPointDict.TryGetValue("pnt0_y", out BYAMLNode? pnt0Y);
            railPointDict.TryGetValue("pnt0_z", out BYAMLNode? pnt0Z);

            railPointDict.TryGetValue("pnt1_x", out BYAMLNode? pnt1X);
            railPointDict.TryGetValue("pnt1_y", out BYAMLNode? pnt1Y);
            railPointDict.TryGetValue("pnt1_z", out BYAMLNode? pnt1Z);

            railPointDict.TryGetValue("pnt2_x", out BYAMLNode? pnt2X);
            railPointDict.TryGetValue("pnt2_y", out BYAMLNode? pnt2Y);
            railPointDict.TryGetValue("pnt2_z", out BYAMLNode? pnt2Z);

            switch (pointType)
            {
                case RailPointType.Bezier:
                    railObj.Points.Add(
                        new RailPointBezier()
                        {
                            ID = pointID?.GetValueAs<int>() ?? i,
                            Point0Trans = new(
                                pnt0X?.GetValueAs<float>() ?? 0,
                                pnt0Y?.GetValueAs<float>() ?? 0,
                                pnt0Z?.GetValueAs<float>() ?? 0
                            ),
                            Point1Trans = new(
                                pnt1X?.GetValueAs<float>() ?? 0,
                                pnt1Y?.GetValueAs<float>() ?? 0,
                                pnt1Z?.GetValueAs<float>() ?? 0
                            ),
                            Point2Trans = new(
                                pnt2X?.GetValueAs<float>() ?? 0,
                                pnt2Y?.GetValueAs<float>() ?? 0,
                                pnt2Z?.GetValueAs<float>() ?? 0
                            ),
                            Properties = railPointDict
                                .Where(i =>
                                    i.Key != "pnt0_x"
                                    && i.Key != "pnt0_y"
                                    && i.Key != "pnt0_z"
                                    && i.Key != "pnt1_x"
                                    && i.Key != "pnt1_y"
                                    && i.Key != "pnt1_z"
                                    && i.Key != "pnt2_x"
                                    && i.Key != "pnt2_y"
                                    && i.Key != "pnt2_z"
                                    && i.Key != "id"
                                )
                                .ToDictionary(i => i.Key, i => i.Value.Value)
                        }
                    );
                    break;

                case RailPointType.Linear:
                    railObj.Points.Add(
                        new RailPointLinear()
                        {
                            ID = pointID?.GetValueAs<int>() ?? i,
                            Translation = new(
                                pnt0X?.GetValueAs<float>() ?? 0,
                                pnt0Y?.GetValueAs<float>() ?? 0,
                                pnt0Z?.GetValueAs<float>() ?? 0
                            ),
                            Properties = railPointDict
                                .Where(i =>
                                    i.Key != "pnt0_x"
                                    && i.Key != "pnt0_y"
                                    && i.Key != "pnt0_z"
                                    && i.Key != "id"
                                )
                                .ToDictionary(i => i.Key, i => i.Value.Value)
                        }
                    );
                    break;

                default:
                    throw new NotImplementedException(
                        "The given rail point type is not supported."
                    );
            }
        }

        return railObj;
    }

    private static StageObj ReadStageObj(
        BYAMLNode node,
        Dictionary<BYAMLNode, RailObj> processedRails,
        StageObjType objType,
        StageFileType fileType,
        StageObj? parent = null
    )
    {
        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        dict.TryGetValue("l_id", out BYAMLNode? id);
        dict.TryGetValue("name", out BYAMLNode? name);
        dict.TryGetValue("ClassName", out BYAMLNode? className);
        dict.TryGetValue("LayerName", out BYAMLNode? layerName);

        dict.TryGetValue("pos_x", out BYAMLNode? posX);
        dict.TryGetValue("pos_y", out BYAMLNode? posY);
        dict.TryGetValue("pos_z", out BYAMLNode? posZ);

        dict.TryGetValue("dir_x", out BYAMLNode? dirX);
        dict.TryGetValue("dir_y", out BYAMLNode? dirY);
        dict.TryGetValue("dir_z", out BYAMLNode? dirZ);

        dict.TryGetValue("scale_x", out BYAMLNode? scaleX);
        dict.TryGetValue("scale_y", out BYAMLNode? scaleY);
        dict.TryGetValue("scale_z", out BYAMLNode? scaleZ);

        dict.TryGetValue("GenerateParent", out BYAMLNode? generateParent);
        dict.TryGetValue("AreaParent", out BYAMLNode? areaParent);

        dict.TryGetValue("Rail", out BYAMLNode? rail);

        dict.TryGetValue("AreaChildren", out BYAMLNode? areaChildren);
        dict.TryGetValue("GenerateChildren", out BYAMLNode? generateChildren);

        RailObj? railObj = null;

        if (rail is not null && rail.NodeType == BYAMLNodeType.Dictionary)
            processedRails.TryGetValue(rail, out railObj);

        List<StageObj> children = new();

        StageObj stageObj =
            new(parent)
            {
                Type = objType,
                FileType = fileType,
                Translation = new(
                    posX?.GetValueAs<float>() ?? 0,
                    posY?.GetValueAs<float>() ?? 0,
                    posZ?.GetValueAs<float>() ?? 0
                ),
                Rotation = new(
                    dirX?.GetValueAs<float>() ?? 0,
                    dirY?.GetValueAs<float>() ?? 0,
                    dirZ?.GetValueAs<float>() ?? 0
                ),
                Scale = new(
                    scaleX?.GetValueAs<float>() ?? 1,
                    scaleY?.GetValueAs<float>() ?? 1,
                    scaleZ?.GetValueAs<float>() ?? 1
                ),
                Name = name?.GetValueAs<string>() ?? "StageObj",
                Layer = layerName?.GetValueAs<string>() ?? "共通",
                Rail = railObj,
                Children = children,
                Properties = dict.Where(i =>
                        i.Key != "pos_x"
                        && i.Key != "pos_y"
                        && i.Key != "pos_z"
                        && i.Key != "dir_x"
                        && i.Key != "dir_y"
                        && i.Key != "dir_z"
                        && i.Key != "scale_x"
                        && i.Key != "scale_y"
                        && i.Key != "scale_z"
                        && i.Key != "name"
                        && i.Key != "LayerName"
                        && i.Key != "l_id"
                        && i.Key != "Rail"
                        && i.Key != "AreaChildren"
                        && i.Key != "GenerateChildren"
                        && i.Key != "GenerateParent"
                        && i.Key != "AreaParent"
                    )
                    .ToDictionary(i => i.Key, i => i.Value.Value)
            };

        #region Children

        if (areaChildren is not null && areaChildren.NodeType == BYAMLNodeType.Array)
        {
            var childrenArray = areaChildren.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode childNode in childrenArray)
            {
                if (childNode.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObj child = ReadStageObj(
                    childNode,
                    processedRails,
                    StageObjType.AreaChild,
                    fileType,
                    parent: stageObj
                );

                children.Add(child);
            }
        }

        if (generateChildren is not null && generateChildren.NodeType == BYAMLNodeType.Array)
        {
            var childrenArray = generateChildren.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode childNode in childrenArray)
            {
                if (childNode.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObj child = ReadStageObj(
                    childNode,
                    processedRails,
                    StageObjType.Child,
                    fileType,
                    parent: stageObj
                );

                children.Add(child);
            }
        }

        #endregion

        return stageObj;
    }
}
