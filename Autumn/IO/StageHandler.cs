using System.Text;
using Autumn.Storage;
using BYAMLSharp;
using NARCSharp;

namespace Autumn.IO;

internal static class StageHandler
{
    private static Encoding s_encoding = Encoding.GetEncoding("Shift-JIS");

    /// <param name="path">The project's root path.</param>
    public static IEnumerable<Stage> LoadProjectStages(string path)
    {
        path = Path.Join(path, "stages");

        if (!Directory.Exists(path))
            yield break;

        foreach (string file in Directory.EnumerateFiles(path))
            yield return LoadStageFrom(file);
    }

    public static Stage LoadStageFrom(string path)
    {
        Stage stage = YAMLWrapper.Deserialize<Stage>(path);

        return stage;
    }

    /// <param name="path">The project's root path.</param>
    public static void SaveProjectStages(string path, IEnumerable<Stage> stages)
    {
        path = Path.Join(path, "stages");

        Directory.CreateDirectory(path);

        foreach (Stage stage in stages)
            SaveStageTo(Path.Join(path, stage.Name + stage.Scenario + ".yml"), stage);
    }

    public static void SaveStageTo(string path, Stage stage) => YAMLWrapper.Serialize(path, stage);

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
        stage = new(name, scenario);

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

        ImportStage(ref stage, design, map, sound);
        return true;
    }

    private static void ImportStage(
        ref Stage stage,
        NARCFileSystem? design,
        NARCFileSystem? map,
        NARCFileSystem? sound
    )
    {
        if (design is not null)
            ParseStageFile(ref stage, design, StageObjFileType.Design);

        if (map is not null)
            ParseStageFile(ref stage, map, StageObjFileType.Map);

        if (sound is not null)
            ParseStageFile(ref stage, sound, StageObjFileType.Sound);
    }

    private static void ParseStageFile(
        ref Stage stage,
        NARCFileSystem narc,
        StageObjFileType fileType
    )
    {
        byte scenario = stage.Scenario;

        byte[] data;
        BYAML byaml;

        // StageData:

        data = narc.GetFile("StageData.byml");
        byaml = BYAMLParser.Read(data, s_encoding);

        IEnumerable<StageObj> stageObjs = ProcessStageObjs(byaml, fileType);

        stage.StageObjs ??= new();
        stage.StageObjs.AddRange(stageObjs);

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
