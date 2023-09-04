using Autumn.Storage;
using Autumn.Storage.StageObjs;
using BYAMLSharp;
using NARCSharp;

namespace Autumn.IO;

internal static class StageHandler
{
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
        string contents = File.ReadAllText(path);
        Stage stage = YAMLWrapper.Desearialize<Stage>(contents);

        return stage;
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

        return TryImportStage(ref stage, design, map, sound);
    }

    private static bool TryImportStage(
        ref Stage stage,
        NARCFileSystem? design,
        NARCFileSystem? map,
        NARCFileSystem? sound
    )
    {
        byte[] data;
        BYAML byaml;

        // Design ----------------------

        if (design is not null)
        {
            // StageData:

            data = design.GetFile("StageData.byml");
            byaml = BYAMLParser.Read(data);

            IStageObj[] stageObjs = StageObjHandler.ProcessStageObjs(
                byaml,
                StageObjFileType.Design
            );

            stage.StageData.AddRange(stageObjs);

            // TO-DO: Other byamls.
        }

        // Map ----------------------

        if (map is not null)
        {
            // StageData:

            data = map.GetFile("StageData.byml");
            byaml = BYAMLParser.Read(data);

            IStageObj[] stageObjs = StageObjHandler.ProcessStageObjs(byaml, StageObjFileType.Map);

            stage.StageData.AddRange(stageObjs);

            // TO-DO: Other byamls.
        }

        // Sound ----------------------

        if (sound is not null)
        {
            // StageData:

            data = sound.GetFile("StageData.byml");
            byaml = BYAMLParser.Read(data);

            IStageObj[] stageObjs = StageObjHandler.ProcessStageObjs(byaml, StageObjFileType.Sound);

            stage.StageData.AddRange(stageObjs);

            // TO-DO: Other byamls.
        }

        return true;
    }
}