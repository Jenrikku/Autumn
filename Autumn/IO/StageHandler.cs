using System.Text;
using Autumn.Storage;
using Autumn.Storage.StageObjs;
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

        IStageObj[] stageObjs = StageObjHandler.ProcessStageObjs(byaml, fileType);

        stage.AddRange(stageObjs);

        // TO-DO: Other byamls.
    }
}
