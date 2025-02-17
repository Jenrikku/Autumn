using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Autumn.Background;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using BYAMLSharp;
using NARCSharp;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;
using static Autumn.Storage.BgmTable;

namespace Autumn.FileSystems;

internal partial class RomFSHandler
{
    [GeneratedRegex("(.*)(Design|Map|Sound)(\\d+\\b).szs")]
    private static partial Regex StageFileRegex();

    private static readonly Regex _stagesRegex = StageFileRegex();

    private static readonly Encoding s_byamlEncoding = Encoding.GetEncoding("Shift-JIS");

    public string Root { get; }

    private readonly string _stagesPath;
    private readonly string _soundPath;
    private readonly string _actorsPath;
    private readonly string _ccntPath;

    private readonly Dictionary<string, Actor> _cachedActors = new();
    private readonly Dictionary<string, DateTime> _cachedActorsTimestamps = new();

    private ReadOnlyDictionary<string, string>? _creatorClassNameTable = null;
    private BgmTable? _bgmTable = null;
    private SystemDataTable? _GSDTable = null;

    public RomFSHandler(string path)
    {
        Root = path;

        _stagesPath = Path.Join(Root, "StageData");
        _actorsPath = Path.Join(Root, "ObjectData");
        _soundPath = Path.Join(Root, "SoundData");
        _ccntPath = Path.Join(Root, "SystemData", "CreatorClassNameTable.szs");

        // Only really used in ModFS
        Directory.CreateDirectory(_stagesPath);
        Directory.CreateDirectory(_actorsPath);
        Directory.CreateDirectory(_soundPath);
        Directory.CreateDirectory(Path.Join(Root, "SystemData"));
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

    public bool ExistsBgmTable() => File.Exists(Path.Join(_soundPath, "BgmTable.szs"));

    public bool ExistsGSDT() => File.Exists(Path.Join(_actorsPath, "GameSystemDataTable.szs"));

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

    #region Stage Writing

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
                switch (filename)
                {
                    case string s when s.Contains("StageData"): // StageData.byml
                        // Read StageObjs from current stage file:
                        BYAML stageDataByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        IEnumerable<StageObj> stageObjs = ProcessStageObjs(stageDataByaml, fileType);

                        stage.AddStageObjs(stageObjs);
                        break;

                    case string s when s.Contains("StageInfo"):
                        if (!s.Contains($"{scenario}") && s != "StageInfo.byml")
                            break;

                        BYAML stageInfoByaml = BYAMLParser.Read(contents, s_byamlEncoding);

                        if (
                            stageInfoByaml.RootNode is null
                            || stageInfoByaml.RootNode.NodeType != BYAMLNodeType.Dictionary
                        )
                            break;

                        var rootDict = stageInfoByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                        rootDict.TryGetValue("StageTimer", out BYAMLNode? timerN);
                        rootDict.TryGetValue("StageTimerRestart", out BYAMLNode? timerRN);
                        rootDict.TryGetValue("PowerUpItemNum", out BYAMLNode? pupN);
                        rootDict.TryGetValue("FootPrint", out BYAMLNode? fprnt);

                        stage.StageParams.Timer = timerN?.GetValueAs<int>() ?? default;
                        stage.StageParams.RestartTimer = timerRN?.GetValueAs<int>() ?? -1;
                        stage.StageParams.MaxPowerUps = pupN?.GetValueAs<int>() ?? -1;

                        if (fprnt is not null)
                        {
                            stage.StageParams.FootPrint = new();
                            var dict = fprnt.GetValueAs<BYAMLNode[]>()?[0].GetValueAs<Dictionary<string, BYAMLNode>>();

                            if (dict is not null)
                            {
                                dict.TryGetValue("AnimName", out BYAMLNode? aN);
                                dict.TryGetValue("AnimType", out BYAMLNode? aT);

                                stage.StageParams.FootPrint.Material = dict["Material"].GetValueAs<string>()!;
                                stage.StageParams.FootPrint.Model = dict["Model"].GetValueAs<string>()!;
                                stage.StageParams.FootPrint.AnimName = aN?.GetValueAs<string>();
                                stage.StageParams.FootPrint.AnimType = aT?.GetValueAs<string>();
                            }
                        }

                        // Temporary
                        stage.AddAdditionalFile(fileType, filename, contents);
                        break;
                    default:
                        stage.AddAdditionalFile(fileType, filename, contents);
                        break;
                }
            }
        }

        return stage;
    }

    public Actor ReadActor(string name, GLTaskScheduler scheduler)
    {
        string path = Path.Join(_actorsPath, name + ".szs");

        // Return cached actor if valid (not modified externally)
        if (_cachedActors.TryGetValue(path, out Actor? cachedActor))
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            if (_cachedActorsTimestamps.TryGetValue(path, out DateTime oldTimestamp) && oldTimestamp == timestamp)
                return cachedActor;
        }

        Actor actor = new(name);

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
            scheduler.EnqueueGLTask(gl => actor.AddTexture(gl, texture));
        }

        foreach (H3DLUT lut in h3D.LUTs)
        foreach (H3DLUTSampler sampler in lut.Samplers)
        {
            scheduler.EnqueueGLTask(gl => actor.AddLUTTexture(gl, lut.Name, sampler));
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
                    actor.ForceModelNotEmpty();

                    // Obtain the mesh's material by its index.
                    int matIdx = mesh.MaterialIndex;
                    H3DMaterial material = model.Materials[matIdx];

                    // Obtain submesh culling.
                    int meshIdx = model.Meshes.IndexOf(mesh);

                    H3DSubMeshCulling? subMeshCulling = null;

                    if (model.SubMeshCullings.Count > meshIdx)
                        subMeshCulling = model.SubMeshCullings[meshIdx];

                    var skeleton = model.Skeleton;

                    H3DMeshLayer meshLayer = (H3DMeshLayer)i;

                    scheduler.EnqueueGLTask(gl =>
                        actor.AddMesh(gl, meshLayer, mesh, subMeshCulling, material, skeleton)
                    );

                    if (mesh.MetaData is not null)
                    {
                        for (int m = 0; m < mesh.MetaData.Count; m++)
                        {
                            if (mesh.MetaData[m].Name == "OBBox")
                                actor.AABB.BoundBox((H3DBoundingBox)mesh.MetaData[m].Values[0]!);
                        }
                    }
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

    public BgmTable ReadBgmTable()
    {
        if (_bgmTable is null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(Path.Join(_soundPath, "BgmTable.szs"));

            _bgmTable = new();

            if (narc is not null)
            {
                byte[] defaultListData = narc.GetFile("StageDefaultBgmList.byml");
                byte[] listData = narc.GetFile("StageBgmList.byml");
                BYAML defaultListByaml = BYAMLParser.Read(defaultListData, s_byamlEncoding);
                BYAML listByaml = BYAMLParser.Read(listData, s_byamlEncoding);

                if (defaultListByaml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = defaultListByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                    BYAMLNode[] defaultnodes = nd[nd.Keys.ToList()[0]].GetValueAs<BYAMLNode[]>()!;

                    foreach (BYAMLNode node in defaultnodes)
                    {
                        if (node.NodeType != BYAMLNodeType.Dictionary)
                            continue;

                        var d = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        d.TryGetValue("BgmLabel", out BYAMLNode? lbl);
                        d.TryGetValue("Scenario", out BYAMLNode? sc);
                        d.TryGetValue("StageName", out BYAMLNode? name);

                        StageDefaultBgm bgm =
                            new()
                            {
                                Scenario = sc?.GetValueAs<int>() ?? 0,
                                BgmLabel = lbl?.GetValueAs<string>() ?? "",
                                StageName = name?.GetValueAs<string>() ?? "",
                            };

                        _bgmTable.StageDefaultBgmList.Add(bgm);
                    }
                }

                if (listByaml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = listByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                    foreach (string id in nd.Keys)
                    {
                        if (nd[id].NodeType != BYAMLNodeType.Array)
                            continue;

                        BYAMLNode[] nodes = nd[id].GetValueAs<BYAMLNode[]>()!;

                        if (id == "KindNumList")
                        {
                            foreach (BYAMLNode node in nodes)
                            {
                                if (node.NodeType != BYAMLNodeType.Dictionary)
                                    continue;

                                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                                // ignore idx for now
                                _bgmTable.BgmTypes.Add(dict["Kind"].GetValueAs<string>()!);
                            }
                        }
                        else
                        {
                            foreach (BYAMLNode node in nodes)
                            {
                                if (node.NodeType != BYAMLNodeType.Dictionary)
                                    continue;

                                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                                dict.TryGetValue("Scenario", out BYAMLNode? sc);
                                dict.TryGetValue("StageName", out BYAMLNode? name);

                                StageBgm bgm =
                                    new()
                                    {
                                        Scenario = sc?.GetValueAs<int>(),
                                        StageName = name?.GetValueAs<string>() ?? "",
                                    };

                                dict.TryGetValue("LineList", out BYAMLNode? list);

                                if (list?.NodeType == BYAMLNodeType.Dictionary)
                                {
                                    var d = list.GetValueAs<Dictionary<string, BYAMLNode>>();
                                    foreach (string item in d!.Keys)
                                    {
                                        List<KindDefine> e = new();

                                        if (d[item].NodeType != BYAMLNodeType.Array)
                                            continue;

                                        foreach (BYAMLNode bnode in d[item].GetValueAs<BYAMLNode[]>()!)
                                        {
                                            if (bnode.NodeType != BYAMLNodeType.Dictionary)
                                                continue;

                                            KindDefine k = new();

                                            k.Kind = bnode
                                                .GetValueAs<Dictionary<string, BYAMLNode>>()!["Kind"]
                                                .GetValueAs<string>()!;

                                            k.Label = bnode
                                                .GetValueAs<Dictionary<string, BYAMLNode>>()!["Label"]
                                                .GetValueAs<string>()!;

                                            e.Add(k);
                                        }

                                        bgm.LineList.Add(item, e);
                                    }
                                }

                                _bgmTable.StageBgmList.Add(bgm);
                            }
                        }
                    }
                }
            }

            _bgmTable.BgmFiles = Directory
                .EnumerateFiles(Path.Join(_soundPath, "stream"))
                .Select(x => Path.GetFileNameWithoutExtension(x))
                .Order()
                .ToArray();
        }

        //var query = (from s in _bgmTable.StageDefaultBgmList where s.Scenario == 1 select s).ToList();
        //_bgmTable.StageDefaultBgmList.Where(x => x.StageName == "FirstStage").ToList();
        return _bgmTable;
    }

    public SystemDataTable ReadGSDTable()
    {
        if (_GSDTable is null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(Path.Join(_actorsPath, "GameSystemDataTable.szs"));

            _GSDTable = new();
            if (narc is not null)
            {
                byte[] courselistData = narc.GetFile("CourseList.byml");
                BYAML courselistByml = BYAMLParser.Read(courselistData, s_byamlEncoding);

                if (courselistByml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = courselistByml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                    BYAMLNode[] worldnodes = nd[nd.Keys.ToList()[0]].GetValueAs<BYAMLNode[]>()!;
                    foreach (BYAMLNode wnode in worldnodes)
                    {
                        if (wnode.NodeType != BYAMLNodeType.Dictionary)
                            continue;

                        SystemDataTable.WorldDefine world = new();
                        var wdict = wnode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        if (wdict.TryGetValue("Type", out BYAMLNode? wtp))
                            world.WorldType = Enum.Parse<SystemDataTable.WorldTypes>(wtp.GetValueAs<string>()!);

                        BYAMLNode[] stagenodes = wdict["Course"].GetValueAs<BYAMLNode[]>()!;

                        int lvlNum = 1;
                        foreach (BYAMLNode snode in stagenodes)
                        {
                            if (snode.NodeType != BYAMLNodeType.Dictionary)
                                continue;

                            var dict = snode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                            dict.TryGetValue("Type", out BYAMLNode? tp);
                            dict.TryGetValue("Miniature", out BYAMLNode? min);
                            dict.TryGetValue("Scenario", out BYAMLNode? sc);
                            dict.TryGetValue("Stage", out BYAMLNode? st);
                            dict.TryGetValue("CoinCollectNum", out BYAMLNode? ccn);

                            SystemDataTable.StageDefine lvl =
                                new()
                                {
                                    StageType = (SystemDataTable.StageTypes)
                                        Enum.Parse(
                                            typeof(SystemDataTable.StageTypes),
                                            tp?.GetValueAs<string>() ?? string.Empty
                                        ),
                                    Scenario = sc?.GetValueAs<int>() ?? -1,
                                    Miniature = min?.GetValueAs<string>() ?? "",
                                    Stage = st?.GetValueAs<string>() ?? "",
                                    CollectCoinNum = ccn?.GetValueAs<int>() ?? -1,
                                };

                            if (
                                lvl.StageType != SystemDataTable.StageTypes.Dokan
                                && lvl.StageType != SystemDataTable.StageTypes.Empty
                            )
                            {
                                lvl.StageNumber = lvlNum;
                                lvlNum += 1;
                            }

                            world.StageList.Add(lvl);
                        }
                        _GSDTable.WorldList.Add(world);
                    }
                }
            }
        }

        return _GSDTable;
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

        //Debug.Assert(allRailInfos.Count <= 1);

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

        //Debug.Assert(pointType == RailPointType.Bezier);
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

        BYAMLNode[] railPointNodes = railPointsNode?.GetValueAs<BYAMLNode[]>() ?? Array.Empty<BYAMLNode>();

        //Debug.Assert(pointCount?.GetValueAs<int>() == railPointNodes.Length);

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
                                    i.Key != "pnt0_x" && i.Key != "pnt0_y" && i.Key != "pnt0_z" && i.Key != "id"
                                )
                                .ToDictionary(i => i.Key, i => i.Value.Value)
                        }
                    );
                    break;

                default:
                    throw new NotImplementedException("The given rail point type is not supported.");
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

        dict.TryGetValue("SwitchA", out BYAMLNode? switchA);
        dict.TryGetValue("SwitchB", out BYAMLNode? switchB);
        dict.TryGetValue("SwitchDeadOn", out BYAMLNode? switchDead);
        dict.TryGetValue("SwitchAppear", out BYAMLNode? switchAppear);
        dict.TryGetValue("SwitchKill", out BYAMLNode? switchK);

        dict.TryGetValue("ViewId", out BYAMLNode? vId);
        dict.TryGetValue("CameraId", out BYAMLNode? camId);
        dict.TryGetValue("ClippingGroupId", out BYAMLNode? cgId);

        RailObj? railObj = null;

        if (rail is not null && rail.NodeType == BYAMLNodeType.Dictionary)
        {
            processedRails.TryGetValue(rail, out railObj);
            if (railObj is null)
            {
                var rO = ReadRailObj(rail, fileType);
                foreach (RailObj r in processedRails.Values)
                {
                    if (rO == r)
                        railObj = r;
                }

                if (railObj is null)
                    throw new(
                        "The rail inside "
                            + name?.GetValueAs<string>()
                            + " is not referenced in AllRailInfos, please report this issue."
                    );
            }
        }
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
                SwitchA = switchA != null ? switchA.GetValueAs<int>() : -1,
                SwitchB = switchB != null ? switchB.GetValueAs<int>() : -1,
                SwitchAppear = switchAppear != null ? switchAppear.GetValueAs<int>() : -1,
                SwitchDeadOn = switchDead != null ? switchDead.GetValueAs<int>() : -1,
                SwitchKill = switchK != null ? switchK.GetValueAs<int>() : -1,
                ViewId = vId != null ? vId.GetValueAs<int>() : -1,
                CameraId = camId != null ? camId.GetValueAs<int>() : -1,
                ClippingGroupId = cgId != null ? cgId.GetValueAs<int>() : -1,
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
                        && i.Key != "CameraId"
                        && i.Key != "ClippingGroupId"
                        && i.Key != "ViewId"
                        && !i.Key.Contains("Switch")
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

    #endregion

    #region Stage Writing

    public bool WriteStage(Stage stage)
    {
        int currentId = 0;
        // check objects in each stage type (map design sound), then on each type we check each Infos list
        Dictionary<StageFileType, string> paths =
            new()
            {
                { StageFileType.Design, Path.Join(_stagesPath, $"{stage.Name}Design{stage.Scenario}.szs") },
                { StageFileType.Map, Path.Join(_stagesPath, $"{stage.Name}Map{stage.Scenario}.szs") },
                { StageFileType.Sound, Path.Join(_stagesPath, $"{stage.Name}Sound{stage.Scenario}.szs") }
            };

        // check design -> map -> sound
        foreach (StageFileType StageType in Enum.GetValues<StageFileType>())
        {
            Dictionary<string, BYAMLNode> rootdict = new();
            BYAMLNode root = new(rootdict);
            BYAML file = new(root, s_byamlEncoding, default);
            Dictionary<string, BYAMLNode> dict = new();
            StageFile st = stage.GetStageFile(StageType);

            if (st.IsEmpty())
                continue;

            dict.Add("AllInfos", new(BYAMLNodeType.Dictionary));
            dict.Add("AllRailInfos", new(BYAMLNodeType.Dictionary));
            dict.Add("LayerInfos", new(BYAMLNodeType.Array));

            Dictionary<string, BYAMLNode> allInfosDict = new(); // dictionary of arrays of dictionaries
            // design , map, sound StageData.byaml

            currentId = 0;
            var allRailDict = dict["AllRailInfos"].GetValueAs<Dictionary<string, BYAMLNode>>()!;

            allRailDict.Add("RailInfo", new(BYAMLNodeType.Array));
            List<BYAMLNode> railInfo = new(); //= railDict["RailInfo"].GetValueAs<BYAMLNode[]>();
            Dictionary<RailObj, BYAMLNode> railObjNodes = new();

            foreach (RailObj rail in st.GetRailInfos())
            {
                var nodeRail = WriteRail(rail, currentId);
                railInfo.Add(nodeRail);
                railObjNodes.Add(rail, nodeRail);
                currentId++;
            }

            allRailDict["RailInfo"].Value = railInfo.ToArray();
            dict["AllRailInfos"].Value = allRailDict;

            //List<BYAMLNode> layerInfos = new();
            //Dictionary<string, BYAMLNode> layerInfosNames = new(); // "Common", BYAML_Array[] -> "ObjInfo", "CameraAreaInfo"...
            Dictionary<string, Dictionary<string, List<BYAMLNode>>> layerInfosList = new(); // "Common", List_Array[] -> "ObjInfo", "CameraAreaInfo"...

            //currentLayer
            currentId = 0;

            foreach (StageObjType Infos in Enum.GetValues<StageObjType>())
            {
                if (Infos != StageObjType.Rail && Infos != StageObjType.Child && Infos != StageObjType.AreaChild)
                {
                    /* AllInfos (dictionary)
            (Dict entry) -> "CameraAreaInfo"[] (array)
                        (array entry 0) -> CameraArea (dictionary)
                        (array entry 1) -> CameraArea (dictionary)
                        (array entry 2) -> CameraArea (dictionary)
            (Dict entry) -> "StartInfo"[] (array)
                        (array entry) -> Mario (dictionary)
                    */

                    List<StageObj> objs = st.GetObjInfos(Infos);

                    if (objs.Count == 0)
                        continue;

                    List<BYAMLNode> currentInfosList = new();
                    foreach (StageObj currentObj in objs)
                    {
                        if (currentObj.Parent != null)
                            continue;

                        string scenarioLayer = string.Empty;
                        int cId = currentId;
                        if (currentObj.Layer.Contains("シナリオ"))
                        {
                            scenarioLayer = "Scenario";
                            if (currentObj.Layer.Contains('＆') || currentObj.Layer.Contains("&"))
                            {
                                scenarioLayer += currentObj.Layer.ElementAt(4) + "And" + currentObj.Layer.ElementAt(6);
                            }
                            else
                            {
                                scenarioLayer += currentObj.Layer.ElementAt(4);
                                // add to ScenarioX
                            }
                        }
                        else if (currentObj.Layer.Contains("共通")) // common / commons
                        {
                            scenarioLayer = "Common";
                            if (currentObj.Layer.Contains("サブ"))
                            {
                                scenarioLayer += "Sub";
                                // add to CommonSub
                            }
                        }
                        else if (currentObj.Layer == string.Empty)
                        { // if we have an incorrect layer we default to Common
                            scenarioLayer = "Common";
                            currentObj.Layer = "共通";
                        }
                        else // if the layer is valid but not predefined we let it save as is
                        {
                            scenarioLayer = currentObj.Layer;
                        }

                        var scObj = WriteSceneObjects(currentObj, ref currentId, railObjNodes);
                        if (scenarioLayer != "")
                        {
                            if (!layerInfosList.ContainsKey(scenarioLayer))
                            {
                                BYAMLNode n = MakeNewLayerInfos(scenarioLayer);
                                layerInfosList.Add(scenarioLayer, new());
                                layerInfosList[scenarioLayer].Add(InfosToString(Infos), new());
                            }
                            else if (!layerInfosList[scenarioLayer].ContainsKey(InfosToString(Infos)))
                            {
                                layerInfosList[scenarioLayer].Add(InfosToString(Infos), new());
                            }
                        }

                        layerInfosList[scenarioLayer][InfosToString(Infos)].Add(scObj);
                        currentInfosList.Add(scObj);
                        currentId += 1;
                    }

                    BYAMLNode currentInfos = new(BYAMLNodeType.Array, currentInfosList.ToArray());
                    string ObjectType = InfosToString(Infos);
                    allInfosDict.Add(ObjectType, currentInfos);
                }
            }

            dict["AllInfos"].Value = allInfosDict.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary();
            BYAMLNode[] layInfos = GetLayerInfos(
                layerInfosList.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary()
            );
            dict["LayerInfos"].Value = layInfos;
            rootdict = dict;
            root.Value = rootdict;
            file.RootNode = root;

            NARCFileSystem narcFS = new(new());
            byte[] binFile = BYAMLParser.Write(file);
            foreach (var (key, value) in st.EnumerateAdditionalFiles())
            {
                narcFS.AddFile(key, value);
            }
            // if (st.StageFileType == StageFileType.Map)
            // {
            //     narcFS.AddFileRoot("StageInfo" + stage.Scenario + ".byml", BYAMLParser.Write(MakeStageInfo(stage)));
            // }
            narcFS.AddFileRoot("StageData.byml", binFile);
            byte[] compressedFile = Yaz0Wrapper.Compress(NARCParser.Write(narcFS.ToNARC()));
#if DEBUG
            if (st.StageFileType == StageFileType.Map)
                File.WriteAllBytes(Path.Join(paths[StageType] + "_StageData.byml"), binFile);
#endif
            File.WriteAllBytes(paths[StageType], compressedFile);
        }
        return true;
    }

    private BYAML MakeStageInfo(Stage stage)
    {
        BYAMLNode root;
        Dictionary<string, BYAMLNode> rd = new();
        if (stage.StageParams.Timer > 0)
        {
            rd.Add("StageTimer", new(BYAMLNodeType.Int, stage.StageParams.Timer));
        }
        if (stage.StageParams.RestartTimer > 0)
        {
            rd.Add("StageTimerRestart", new(BYAMLNodeType.Int, stage.StageParams.RestartTimer));
        }
        if (stage.StageParams.MaxPowerUps > 0)
        {
            rd.Add("PowerUpItemNum", new(BYAMLNodeType.Int, stage.StageParams.MaxPowerUps));
        }

        root = new(rd);
        BYAML ret = new(root, s_byamlEncoding, default);
        ret.RootNode = root;
        return ret;
    }

    private BYAMLNode MakeNewLayerInfos(string layerName, BYAMLNode? layerInfosDict = null)
    {
        BYAMLNode kvp0;
        if (layerInfosDict is null)
            kvp0 = new(BYAMLNodeType.Dictionary);
        else
            kvp0 = new(layerInfosDict);
        BYAMLNode kvp1 = new(BYAMLNodeType.String, layerName);
        Dictionary<string, BYAMLNode> newdict = new() { { "Infos", kvp0 }, { "LayerName", kvp1 } };
        return new(newdict);
    }

    private BYAMLNode[] GetLayerInfos(Dictionary<string, Dictionary<string, List<BYAMLNode>>> layerInfosList)
    {
        List<BYAMLNode> retArray = new();
        foreach (string key in layerInfosList.Keys)
        {
            Dictionary<string, BYAMLNode> currentInfo = new();
            List<string> sl = layerInfosList[key].Keys.ToList();

            foreach (string infodict in sl)
            {
                currentInfo.Add(infodict, new(BYAMLNodeType.Array, layerInfosList[key][infodict].ToArray()));
            }

            BYAMLNode kvp0 = new(currentInfo);
            BYAMLNode kvp1 = new(BYAMLNodeType.String, key);
            Dictionary<string, BYAMLNode> newdict = new() { { "Infos", kvp0 }, { "LayerName", kvp1 } };
            retArray.Add(new(newdict));
        }

        return retArray.ToArray();
    }

    public static string InfosToString(StageObjType _infos)
    {
        return _infos switch
        {
            StageObjType.Regular => "ObjInfo",
            StageObjType.Area => "AreaObjInfo",
            StageObjType.CameraArea => "CameraAreaInfo",
            StageObjType.Goal => "GoalObjInfo",
            StageObjType.Start => "StartInfo",
            StageObjType.StartEvent => "StartEventObjInfo",
            StageObjType.DemoScene => "DemoSceneObjInfo",

            StageObjType.Child => "Child Object",
            StageObjType.AreaChild => "Area Child Object",
            StageObjType.Rail => "Rail",
            _ => throw new("Incorrect object type")
        };
    }

    private BYAMLNode WriteRail(RailObj rail, int currentId)
    {
        Dictionary<string, BYAMLNode> currentRailNodes = new();
        // try to get Args
        for (int i = 0; i < 10; i++)
        {
            rail.Properties.TryGetValue("Arg" + i, out object? arg);
            if (arg != null)
                currentRailNodes.Add("Arg" + i, new(BYAMLNodeType.Int, arg));
        }
        // try to get LayerName
        currentRailNodes.Add("LayerName", new(BYAMLNodeType.String, rail.Layer));
        // try to get MultiFileName
        rail.Properties.TryGetValue("MultiFileName", out object? mfn);
        if (mfn != null)
            currentRailNodes.Add("MultiFileName", new(BYAMLNodeType.String, mfn));
        // try to get Points
        //rail.Points;
        List<BYAMLNode> points = new();
        int ptid = 0;
        foreach (RailPoint pt in rail.Points)
        {
            // try to get point Args
            Dictionary<string, BYAMLNode> ptd = new();
            for (int i = 0; i < 10; i++)
            {
                pt.Properties.TryGetValue("Arg" + i, out object? arg);
                if (arg != null)
                    ptd.Add("Arg" + i, new(BYAMLNodeType.Int, arg));
            }
            // try to get point id
            ptd.Add("id", new(BYAMLNodeType.Int, ptid));

            // try to get point positions
            if (rail.PointType == RailPointType.Linear)
            {
                var lpt = (RailPointLinear)pt;
                for (int p = 0; p < 3; p++)
                {
                    ptd.Add("pnt" + p + "_x", new(BYAMLNodeType.Float, lpt.Translation.X));
                    ptd.Add("pnt" + p + "_y", new(BYAMLNodeType.Float, lpt.Translation.Y));
                    ptd.Add("pnt" + p + "_z", new(BYAMLNodeType.Float, lpt.Translation.Z));
                }
            }
            else
            {
                var bpt = (RailPointBezier)pt;
                ptd.Add("pnt0_x", new(BYAMLNodeType.Float, bpt.Point0Trans.X));
                ptd.Add("pnt0_y", new(BYAMLNodeType.Float, bpt.Point0Trans.Y));
                ptd.Add("pnt0_z", new(BYAMLNodeType.Float, bpt.Point0Trans.Z));
                ptd.Add("pnt1_x", new(BYAMLNodeType.Float, bpt.Point1Trans.X));
                ptd.Add("pnt1_y", new(BYAMLNodeType.Float, bpt.Point1Trans.Y));
                ptd.Add("pnt1_z", new(BYAMLNodeType.Float, bpt.Point1Trans.Z));
                ptd.Add("pnt2_x", new(BYAMLNodeType.Float, bpt.Point2Trans.X));
                ptd.Add("pnt2_y", new(BYAMLNodeType.Float, bpt.Point2Trans.Y));
                ptd.Add("pnt2_z", new(BYAMLNodeType.Float, bpt.Point2Trans.Z));
            }
            points.Add(new(ptd));
            ptid += 1;
        }
        // add points to rail
        currentRailNodes.Add("Points", new(BYAMLNodeType.Array, points.ToArray()));
        // try to get closed
        currentRailNodes.Add("closed", new(BYAMLNodeType.String, rail.Closed ? "CLOSE" : "OPEN", false));
        // try to get l_id
        rail.RailNo = currentId;
        currentRailNodes.Add("l_id", new(BYAMLNodeType.Int, currentId));
        // try to get name
        currentRailNodes.Add("name", new(BYAMLNodeType.String, rail.Name));
        currentRailNodes.Add("no", new(BYAMLNodeType.Int, currentId));
        // try to get num_pnt
        currentRailNodes.Add("num_pnt", new(BYAMLNodeType.Int, rail.Points.Count()));
        // try to get type
        currentRailNodes.Add("type", new(BYAMLNodeType.String, rail.PointType.ToString()));
        return new BYAMLNode(currentRailNodes);
    }

    private BYAMLNode WriteSceneObjects(
        StageObj currentObj,
        ref int currentId,
        Dictionary<RailObj, BYAMLNode> railObjNodes,
        int parentId = -1
    )
    {
        // read all information
        Dictionary<string, BYAMLNode> currentObjectNodes = new();
        //WriteSceneObjects(stageObj, currentId, file);
        int m = 10;

        if (currentObj.Type != StageObjType.Regular)
            m = 8;

        for (int i = 0; i < m; i++)
        {
            currentObj.Properties.TryGetValue("Arg" + i, out object? arg);

            if (arg != null)
                currentObjectNodes.Add("Arg" + i, new(BYAMLNodeType.Int, arg));
        }

        foreach (var (name, property) in currentObj.Properties)
        {
            if (name.Contains("Arg"))
                continue;

            if (property is null)
                continue;

            switch (property)
            {
                case object p when p is int:
                    int intBuf = (int)(p ?? -1);
                    currentObjectNodes.Add(name, new(BYAMLNodeType.Int, intBuf));
                    break;

                case object p when p is string:
                    string strBuf = (string)(p ?? string.Empty);
                    currentObjectNodes.Add(name, new(BYAMLNodeType.String, strBuf));
                    break;

                default:
                    throw new NotImplementedException(
                        "The property type " + property?.GetType().FullName ?? "null" + " is not supported."
                    );
            }
        }
        if (currentObj.Name != "Mario")
            currentObjectNodes.Add("l_id", new(BYAMLNodeType.Int, currentId));

        if (currentObj.Children != null && currentObj.Children.Any())
        {
            List<BYAMLNode> objectArray = new();
            List<BYAMLNode> areaArray = new();
            // recursive function
            int pid = currentId;
            foreach (StageObj child in currentObj.Children)
            {
                currentId += 1;
                if (child.Type == StageObjType.AreaChild)
                    areaArray.Add(WriteSceneObjects(child, ref currentId, railObjNodes, pid));
                else
                    objectArray.Add(WriteSceneObjects(child, ref currentId, railObjNodes, pid));
            }

            BYAMLNode objectArrayNode = new(BYAMLNodeType.Array, objectArray.ToArray());
            BYAMLNode areaArrayNode = new(BYAMLNodeType.Array, areaArray.ToArray());

            if (objectArray.Count != 0)
                currentObjectNodes.Add("GenerateChildren", objectArrayNode);

            if (areaArray.Count != 0)
                currentObjectNodes.Add("AreaChildren", areaArrayNode);
        }

        currentObjectNodes.Add("name", new(BYAMLNodeType.String, currentObj.Name));

        if (currentObj.ClassName != null)
            currentObjectNodes.Add("ClassName", new(BYAMLNodeType.String, currentObj.ClassName));

        currentObjectNodes.Add("LayerName", new(BYAMLNodeType.String, currentObj.Layer));

        // object transforms
        {
            currentObjectNodes.Add("pos_x", new(BYAMLNodeType.Float, currentObj.Translation.X));
            currentObjectNodes.Add("pos_y", new(BYAMLNodeType.Float, currentObj.Translation.Y));
            currentObjectNodes.Add("pos_z", new(BYAMLNodeType.Float, currentObj.Translation.Z));

            currentObjectNodes.Add("dir_x", new(BYAMLNodeType.Float, currentObj.Rotation.X));
            currentObjectNodes.Add("dir_y", new(BYAMLNodeType.Float, currentObj.Rotation.Y));
            currentObjectNodes.Add("dir_z", new(BYAMLNodeType.Float, currentObj.Rotation.Z));

            currentObjectNodes.Add("scale_x", new(BYAMLNodeType.Float, currentObj.Scale.X));
            currentObjectNodes.Add("scale_y", new(BYAMLNodeType.Float, currentObj.Scale.Y));
            currentObjectNodes.Add("scale_z", new(BYAMLNodeType.Float, currentObj.Scale.Z));
        }

        if (currentObj.Type != StageObjType.Start)
        {
            currentObjectNodes.Add("SwitchA", new(BYAMLNodeType.Int, currentObj.SwitchA));
            currentObjectNodes.Add("SwitchAppear", new(BYAMLNodeType.Int, currentObj.SwitchAppear));
            currentObjectNodes.Add("SwitchB", new(BYAMLNodeType.Int, currentObj.SwitchB));
            currentObjectNodes.Add("SwitchDeadOn", new(BYAMLNodeType.Int, currentObj.SwitchDeadOn));
            currentObjectNodes.Add("SwitchKill", new(BYAMLNodeType.Int, currentObj.SwitchKill));
        }

        if (
            currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.Child
            || currentObj.Type == StageObjType.Goal
        )
        {
            currentObjectNodes.Add("ViewId", new(BYAMLNodeType.Int, currentObj.ViewId));
            currentObjectNodes.Add("ClippingGroupId", new(BYAMLNodeType.Int, currentObj.ClippingGroupId));
        }

        if (
            currentObj.Type == StageObjType.Goal
            || currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.CameraArea
            || currentObj.Type == StageObjType.Child
        )
        {
            currentObjectNodes.Add("CameraId", new(BYAMLNodeType.Int, currentObj.CameraId));
        }

        if (currentObj.Rail is not null)
        {
            currentObjectNodes.Add("Rail", railObjNodes[currentObj.Rail]);
        }

        if (currentObj.Type == StageObjType.Area || currentObj.Type == StageObjType.AreaChild)
            currentObjectNodes.Add("AreaParent", new(BYAMLNodeType.Int, currentObj.Parent != null ? parentId : -1));
        else if (
            currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.Goal
            || currentObj.Type == StageObjType.Child
        )
            currentObjectNodes.Add("GenerateParent", new(BYAMLNodeType.Int, currentObj.Parent != null ? parentId : -1));

        return new(currentObjectNodes);
    }

    #endregion
}
