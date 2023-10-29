using System.Diagnostics;
using System.Text;
using Autumn.Storage;
using BYAMLSharp;
using NARCSharp;

namespace Autumn.IO;

internal static class StageHandler
{
    private static readonly Encoding s_encoding = Encoding.GetEncoding("Shift-JIS");

    public static void LoadProjectStage(Stage stage)
    {
        if (!ProjectHandler.ProjectLoaded)
            return;

        string stageDir = Path.Join(
            ProjectHandler.ActiveProject.SavePath,
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
            stage.StageData = YAMLWrapper.Deserialize<List<StageObj>>(yamlPath);

        // TO-DO: Other yamls.

        stage.Loaded = true;
    }

    public static void SaveProjectStage(Stage stage)
    {
        if (!ProjectHandler.ProjectLoaded)
            return;

        string stageDir = Path.Join(
            ProjectHandler.ActiveProject.SavePath,
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

        // TO-DO: Other yamls.
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

        // TO-DO: Other byamls.
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
                    Properties = dict.Where(
                            i =>
                                i.Key != "no"
                                && i.Key != "closed"
                                && i.Key != "type"
                                && i.Key != "num_pnt"
                                && i.Key != "Points"
                                && i.Key != "name"
                                && i.Key != "LayerName"
                                && i.Key != "l_id"
                        )
                        .ToDictionary(i => i.Key, i => new StageObjProperty(i.Value.Value))
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

                railPointDict.TryGetValue("l_id", out BYAMLNode? pointID);

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
                                    .Where(
                                        i =>
                                            i.Key != "pnt0_x"
                                            && i.Key != "pnt0_y"
                                            && i.Key != "pnt0_z"
                                            && i.Key != "pnt1_x"
                                            && i.Key != "pnt1_y"
                                            && i.Key != "pnt1_z"
                                            && i.Key != "pnt2_x"
                                            && i.Key != "pnt2_y"
                                            && i.Key != "pnt2_z"
                                            && i.Key != "l_id"
                                    )
                                    .ToDictionary(
                                        i => i.Key,
                                        i => new StageObjProperty(i.Value.Value)
                                    )
                            }
                        );
                        break;

                    default:
                        throw new NotImplementedException(
                            "The given rail point type is not supported."
                        );
                }
            }

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

                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                dict.TryGetValue("l_id", out BYAMLNode? id);
                dict.TryGetValue("name", out BYAMLNode? name);
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

                dict.TryGetValue("Rail", out BYAMLNode? rail);

                RailObj? railObj = null;

                if (rail is not null && rail.NodeType == BYAMLNodeType.Dictionary)
                    processedRails.TryGetValue(rail, out railObj);

                yield return new()
                {
                    Type = info.Key switch
                    {
                        "AreaObjInfo" => StageObjType.Area,
                        "CameraAreaInfo" => StageObjType.CameraArea,
                        "GoalObjInfo" => StageObjType.Goal,
                        "ObjInfo" => StageObjType.Regular,
                        "StartEventObjInfo" => StageObjType.StartEvent,
                        "StartInfo" => StageObjType.Start,
                        "DemoSceneObjInfo" => StageObjType.DemoScene,
                        _ => throw new NotSupportedException("Unknown stage obj type found.")

                        // There may be more to be seen.
                    },
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
                    Layer = layerName?.GetValueAs<string>() ?? "共通",
                    ID = id?.GetValueAs<int>() ?? -1,
                    Rail = railObj,
                    Properties = dict.Where(
                            i =>
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
                        )
                        .ToDictionary(i => i.Key, i => new StageObjProperty(i.Value.Value))
                };
            }
        }
    }
}
