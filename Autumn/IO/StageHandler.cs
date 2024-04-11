using System.Diagnostics;
using System.Numerics;
using System.Text;
using Autumn.Storage;
using BYAMLSharp;
using BYAMLSharp.Ext;
using NARCSharp;
using NewGear.Trees.TrueTree;

namespace Autumn.IO;

internal static class StageHandler
{
    private static readonly Encoding s_encoding = Encoding.GetEncoding("Shift-JIS");

    public static Stage CreateNewStage(string name, byte scenario = 1)
    {
        Stage stage = new(name, scenario);

        // Add a Mario.
        StageObj startStageObj = new() { Type = StageObjType.Start, Name = "Mario" };
        startStageObj.Properties.Add("MarioNo", 0);
        if (ProjectHandler.UseClassNames)
            startStageObj.ClassName = "Mario";

        stage.StageData ??= new();
        stage.StageData.Add(startStageObj);

        return stage;
    }

    public static void LoadProjectStage(Stage stage)
    {
        if (!ProjectHandler.ProjectLoaded)
            return;

        string stageDir = Path.Join(
            ProjectHandler.ProjectSavePath,
            "stages",
            stage.Name + stage.Scenario
        );

        LoadStageFrom(stage, stageDir);
    }

    public static void LoadStageFrom(Stage stage, string path)
    {
        if (stage.Loaded)
            return;

        string yamlPath;

        yamlPath = Path.Join(path, "StageData.yml");

        if (File.Exists(yamlPath))
        {
            var root = YAMLWrapper.Deserialize<List<object>>(yamlPath);

            if (root is not null)
            {
                stage.StageData ??= new();
                stage.StageData.Clear();

                foreach (object obj in root)
                {
                    Type t = obj.GetType();

                    if (obj is not Dictionary<object, object> dict)
                        continue;

                    StageObj stageObj = ReadStageObj(dict);

                    stage.StageData.Add(stageObj);
                }
            }
        }

        yamlPath = Path.Join(path, "PreLoadFileList.yml");

        if (File.Exists(yamlPath))
        {
            var root = YAMLWrapper.Deserialize<List<object?>>(yamlPath);
            stage.PreLoadFileList = root;
        }

        // Other yamls.

        foreach (string file in Directory.EnumerateFiles(path))
        {
            if (file == "StageData.yml" || file == "PreLoadFileList.yml")
                continue;

            var root = YAMLWrapper.Deserialize<Dictionary<string, object?>>(yamlPath);
            stage.OtherFiles.Add(Path.GetFileNameWithoutExtension(file), root ?? new());
        }

        stage.Loaded = true;
    }

    public static void SaveProjectStage(Stage stage)
    {
        if (!ProjectHandler.ProjectLoaded)
            return;

        string stageDir = Path.Join(
            ProjectHandler.ProjectSavePath,
            "stages",
            stage.Name + stage.Scenario
        );

        SaveStageTo(stage, stageDir);
        stage.Saved = true;
    }

    public static void SaveStageTo(Stage stage, string path)
    {
        if (!stage.Loaded)
            return;

        Directory.CreateDirectory(path);

        string yamlPath;

        if (stage.StageData is not null)
        {
            yamlPath = Path.Join(path, "StageData.yml");
            YAMLWrapper.Serialize(yamlPath, stage.StageData);
        }

        if (stage.PreLoadFileList is not null)
        {
            yamlPath = Path.Join(path, "PreLoadFileList.yml");
            YAMLWrapper.Serialize(yamlPath, stage.PreLoadFileList);
        }

        foreach (var (name, dict) in stage.OtherFiles)
        {
            yamlPath = Path.Join(path, name + ".yml");
            YAMLWrapper.Serialize(yamlPath, dict);
        }
    }

    public static bool TryImportStage(string name, byte scenario, out Stage stage) =>
        TryImportStageFrom(
            Path.Join(RomFSHandler.RomFSPath, "StageData"),
            name,
            scenario,
            out stage
        );

    public static bool TryImportStageFrom(
        string directory,
        string name,
        byte scenario,
        out Stage stage
    )
    {
        stage = new(name, scenario) { Loaded = true };

        string path;
        byte[] data;

        NARCFileSystem? design = null;
        NARCFileSystem? map = null;
        NARCFileSystem? sound = null;

        path = Path.Join(directory, name + "Design" + scenario + ".szs");

        if (Path.Exists(path))
        {
            data = File.ReadAllBytes(path);
            design = SZSWrapper.ReadFile(data);
        }

        path = Path.Join(directory, name + "Map" + scenario + ".szs");

        if (Path.Exists(path))
        {
            data = File.ReadAllBytes(path);
            map = SZSWrapper.ReadFile(data);
        }

        path = Path.Join(directory, name + "Sound" + scenario + ".szs");

        if (Path.Exists(path))
        {
            data = File.ReadAllBytes(path);
            sound = SZSWrapper.ReadFile(data);
        }

        if (design is null && map is null && sound is null)
            return false;

        ImportStage(stage, design, map, sound);
        return true;
    }

    private static void ImportStage(
        Stage stage,
        NARCFileSystem? design,
        NARCFileSystem? map,
        NARCFileSystem? sound
    )
    {
        if (design is not null)
            ParseStageFile(stage, design, StageObjFileType.Design);

        if (map is not null)
            ParseStageFile(stage, map, StageObjFileType.Map);

        if (sound is not null)
            ParseStageFile(stage, sound, StageObjFileType.Sound);
    }

    private static void ParseStageFile(Stage stage, NARCFileSystem narc, StageObjFileType fileType)
    {
        byte scenario = stage.Scenario;

        byte[] data;
        BYAML byaml;

        // StageData:

        data = narc.GetFile("StageData.byml");
        byaml = BYAMLParser.Read(data, s_encoding);

        IEnumerable<StageObj> stageObjs = ProcessStageObjs(byaml, fileType);

        stage.StageData ??= new();
        stage.StageData.AddRange(stageObjs);

        // PreLoadFileList (unfinished)

        data = narc.GetFile($"PreLoadFileList{stage.Scenario}.byml");

        if (data.Length > 0)
        {
            byaml = BYAMLParser.Read(data, s_encoding);

            List<object?>? preLoadList = byaml.RootNode.AsObjectList();

            stage.PreLoadFileList = preLoadList;
        }

        // Other byamls.

        foreach (LeafNode<byte[]> leaf in narc.ToNARC().RootNode.ChildLeaves)
        {
            if (
                leaf.Name == "StageData.byml"
                || leaf.Name.StartsWith("PreLoadFileList")
                || !leaf.Name.EndsWith(".byml")
            )
                continue;

            data = leaf.Contents ?? Array.Empty<byte>();
            byaml = BYAMLParser.Read(data, s_encoding);

            BYAMLNode root = byaml.RootNode;

            Debug.Assert(root.NodeType == BYAMLNodeType.Dictionary);

            var dict = root.AsObjectDictionary();

            stage.OtherFiles.Add(leaf.Name.Replace(".byml", null), dict!);
        }
    }

    private static IEnumerable<StageObj> ProcessStageObjs(BYAML byaml, StageObjFileType fileType)
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

    private static RailObj ReadRailObj(BYAMLNode node, StageObjFileType fileType)
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
                ID = id?.GetValueAs<int>() ?? -1,
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
        StageObjFileType fileType
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

        #region Children

        List<StageObj> children = new();

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
                    fileType
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
                    fileType
                );

                children.Add(child);
            }
        }

        bool useClassNames = ProjectHandler.UseClassNames;
        string? classNameRes = null;

        if (useClassNames)
        {
            classNameRes = className?.GetValueAs<string>();

            if (classNameRes is null && name is not null)
            {
                RomFSHandler.CreatorClassNameTable.TryGetValue(
                    name.GetValueAs<string>()!,
                    out classNameRes
                );
            }

            classNameRes ??= string.Empty;
        }

        #endregion

        StageObj stageObj =
            new()
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
                    scaleX?.GetValueAs<float>() ?? 0,
                    scaleY?.GetValueAs<float>() ?? 0,
                    scaleZ?.GetValueAs<float>() ?? 0
                ),
                Name = name?.GetValueAs<string>() ?? "StageObj",
                ClassName = classNameRes,
                Layer = layerName?.GetValueAs<string>() ?? "共通",
                ID = id?.GetValueAs<int>() ?? -1,
                ParentID = generateParent?.GetValueAs<int>() ?? areaParent?.GetValueAs<int>() ?? -1,
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
                        && (!useClassNames || i.Key != "ClassName")
                    )
                    .ToDictionary(i => i.Key, i => i.Value.Value)
            };

        stageObj.SetRail(railObj);

        return stageObj;
    }

    // Used when reading a stage data yaml.
    private static StageObj ReadStageObj(Dictionary<object, object> dict)
    {
        List<StageObj>? children = null;

        List<RailPoint>? points = null;

        StageObjFileType fileType = default;
        StageObjType type = default;
        RailPointType pointType = default;

        int id = -1;
        int parentId = -1;
        int railId = -1;
        int railNo = 0;

        string? layer = null;
        string? name = null;
        string? className = null;

        bool closed = false;

        Vector3 translation = new();
        Vector3 rotation = new();
        Vector3 scale = new(1);

        Dictionary<string, object?>? properties = null;

        foreach (var (key, value) in dict)
        {
            string keyStr = key.ToString() ?? string.Empty;

            switch (keyStr, value)
            {
                case var (s, o) when s == "Children" && o is List<object> list:
                    foreach (object item in list)
                    {
                        if (item is not Dictionary<object, object> itemDict)
                            continue;

                        StageObj child = ReadStageObj(itemDict);

                        children ??= new();
                        children.Add(child);
                    }

                    break;

                case var (s, o) when s == "Points" && o is List<object> list:
                    foreach (object item in list)
                    {
                        if (item is not Dictionary<object, object> itemDict)
                            continue;

                        RailPoint point = ReadRailPoint(dict);

                        points ??= new();
                        points.Add(point);
                    }

                    break;

                case var (s, o) when s == "FileType" && o is string str:
                    fileType = Enum.Parse<StageObjFileType>(str);
                    break;

                case var (s, o) when s == "Type" && o is string str:
                    type = Enum.Parse<StageObjType>(str);
                    break;

                case var (s, o) when s == "PointType" && o is string str:
                    pointType = Enum.Parse<RailPointType>(str);
                    break;

                case var (s, o) when s == "ID" && o is int no:
                    id = no;
                    break;

                case var (s, o) when s == "ParentID" && o is int no:
                    parentId = no;
                    break;

                case var (s, o) when s == "RailID" && o is int no:
                    railId = no;
                    break;

                case var (s, o) when s == "RailNo" && o is int no:
                    railNo = no;
                    break;

                case var (s, o) when s == "Layer" && o is string str:
                    layer = str;
                    break;

                case var (s, o) when s == "Name" && o is string str:
                    name = str;
                    break;

                case var (s, o) when s == "ClassName" && o is string str:
                    className = str;
                    break;

                case var (s, o) when s == "Closed" && o is bool b:
                    closed = b;
                    break;

                case var (s, o)
                    when s == "Translation" && o is Dictionary<object, object> childDict:
                    ReadVector3(childDict, ref translation);
                    break;

                case var (s, o) when s == "Rotation" && o is Dictionary<object, object> childDict:
                    ReadVector3(childDict, ref rotation);
                    break;

                case var (s, o) when s == "Scale" && o is Dictionary<object, object> childDict:
                    ReadVector3(childDict, ref scale);
                    break;

                case var (s, o) when s == "Properties" && o is Dictionary<object, object> childDict:
                    properties = ReadProperties(childDict);
                    break;
            }
        }

        if (type == StageObjType.Rail)
        {
            return new RailObj()
            {
                Children = children,
                Points = points ?? new(),
                FileType = fileType,
                Type = type,
                PointType = pointType,
                ID = id,
                ParentID = parentId,
                RailID = railId,
                RailNo = railNo,
                Layer = layer ?? "共通",
                Name = name ?? "Rail",
                Closed = closed,
                Translation = translation,
                Rotation = rotation,
                Scale = scale,
                Properties = properties ?? new()
            };
        }

        bool useClassNames = ProjectHandler.UseClassNames;

        if (useClassNames && className is null)
        {
            if (name is not null)
                RomFSHandler.CreatorClassNameTable.TryGetValue(name, out className);

            className ??= string.Empty;
        }

        if (!useClassNames && className is not null)
        {
            properties ??= new();
            properties.Add("ClassName", className);
        }

        return new StageObj()
        {
            Children = children,
            FileType = fileType,
            Type = type,
            ID = id,
            ParentID = parentId,
            RailID = railId,
            Layer = layer ?? "共通",
            Name = name ?? "StageObj",
            ClassName = useClassNames ? className : null,
            Translation = translation,
            Rotation = rotation,
            Scale = scale,
            Properties = properties ?? new()
        };
    }

    private static RailPoint ReadRailPoint(Dictionary<object, object> dict)
    {
        RailPoint result;

        // Detect point type:
        if (dict.TryGetValue("Translation", out object? translation))
        {
            // Linear rail point:
            RailPointLinear railPoint = new();

            if (translation is Dictionary<object, object> translationDict)
                ReadVector3(translationDict, ref railPoint.Translation);

            result = railPoint;
        }
        else
        {
            // Bezier rail point:
            RailPointBezier railPoint = new();

            foreach (var (key, value) in dict)
            {
                string keyStr = key.ToString() ?? string.Empty;

                switch (keyStr, value)
                {
                    case var (s, o)
                        when s == "Point0Trans" && o is Dictionary<object, object> childDict:
                        ReadVector3(childDict, ref railPoint.Point0Trans);
                        break;

                    case var (s, o)
                        when s == "Point1Trans" && o is Dictionary<object, object> childDict:
                        ReadVector3(childDict, ref railPoint.Point1Trans);
                        break;

                    case var (s, o)
                        when s == "Point2Trans" && o is Dictionary<object, object> childDict:
                        ReadVector3(childDict, ref railPoint.Point2Trans);
                        break;
                }
            }

            result = railPoint;
        }

        // Read ID:
        if (dict.TryGetValue("ID", out object? id) && id is int idInt)
            result.ID = idInt;

        // Read properties:
        if (
            dict.TryGetValue("Properties", out object? properties)
            && properties is Dictionary<object, object> propertiesDict
        )
            result.Properties = ReadProperties(propertiesDict);

        return result;
    }

    private static void ReadVector3(Dictionary<object, object> dict, ref Vector3 vector)
    {
        foreach (var (key, value) in dict)
        {
            string keyStr = key.ToString() ?? string.Empty;

            switch (keyStr, value)
            {
                case var (s, o) when s == "X" && o is double no:
                    vector.X = (float)no;
                    break;

                case var (s, o) when s == "Y" && o is double no:
                    vector.Y = (float)no;
                    break;

                case var (s, o) when s == "Z" && o is double no:
                    vector.Z = (float)no;
                    break;
            }
        }
    }

    private static Dictionary<string, object?> ReadProperties(Dictionary<object, object> dict)
    {
        Dictionary<string, object?> result = new();

        foreach (var (key, value) in dict)
        {
            string resultKey = key.ToString() ?? string.Empty;
            object resultValue = value;

            if (resultValue is double valueDouble)
                resultValue = (float)valueDouble;

            result.Add(resultKey, resultValue);
        }

        return result;
    }
}
