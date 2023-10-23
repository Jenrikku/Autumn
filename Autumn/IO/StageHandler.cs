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
                        dict["pos_x"].GetValueAs<float>()!,
                        dict["pos_y"].GetValueAs<float>()!,
                        dict["pos_z"].GetValueAs<float>()!
                    ),
                    Rotation = new(
                        dict["dir_x"].GetValueAs<float>()!,
                        dict["dir_y"].GetValueAs<float>()!,
                        dict["dir_z"].GetValueAs<float>()!
                    ),
                    Scale = new(
                        dict["scale_x"].GetValueAs<float>()!,
                        dict["scale_y"].GetValueAs<float>()!,
                        dict["scale_z"].GetValueAs<float>()!
                    ),
                    Name = dict["name"].GetValueAs<string>()!,
                    Layer = dict["LayerName"].GetValueAs<string>()!,
                    ID = id?.GetValueAs<int>() ?? -1,
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
                        )
                        .ToDictionary(i => i.Key, i => new StageObjProperty(i.Value.Value))
                };
            }
        }
    }
}
