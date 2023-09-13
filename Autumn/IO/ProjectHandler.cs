using Autumn.Storage;

namespace Autumn.IO;

internal static class ProjectHandler
{
    public static bool ProjectLoaded { get; private set; } = false;

    public static Project ActiveProject;

    /// <param name="path">The path to the project file.</param>
    public static void LoadProject(string path)
    {
        ActiveProject = YAMLWrapper.Desearialize<Project>(path);

        ActiveProject.SavePath =
            Directory.GetParent(path)?.FullName ?? Directory.GetDirectoryRoot(path);

        IEnumerable<Stage> stages = StageHandler.LoadProjectStages(ActiveProject.SavePath);
        ActiveProject.Stages = new(stages);

        ActiveProject.Objects = new();
        // TO-DO: Objects.

        ProjectLoaded = true;
    }

    public static void CreateNew(string path, string name = "Untitled")
    {
        ActiveProject = new() { Name = name };

        Directory.CreateDirectory(path);

        YAMLWrapper.Serialize(Path.Join(path, "autumnproj.yml"), ActiveProject);
    }

    public static bool FileExists(string path)
    {
        if (!ProjectLoaded)
            return false;

        return File.Exists(Path.Join(ActiveProject.SavePath, path));
    }

    public static bool DirectoryExists(string path)
    {
        if (!ProjectLoaded)
            return false;

        return Directory.Exists(Path.Join(ActiveProject.SavePath, path));
    }
}
