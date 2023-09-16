using System.Diagnostics;
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

        string dir = Path.GetDirectoryName(path) ?? Directory.GetDirectoryRoot(path);

        ActiveProject.SavePath = dir;
        ActiveProject.ProjectFileName = Path.GetFileName(path);

        IEnumerable<Stage> stages = StageHandler.LoadProjectStages(dir);
        ActiveProject.Stages = new(stages);

        ActiveProject.Objects = new();
        // TO-DO: Objects.

        ProjectLoaded = true;
    }

    /// <summary>
    /// Saves the current project.
    /// </summary>
    /// <param name="path">The path to the project file.</param>
    /// <returns>False if the project's save path is null or empty and no path was passed as an argument.</returns>
    public static bool SaveProject(string? path = null)
    {
        string savePath;
        string projectFileName;

        if (string.IsNullOrEmpty(ActiveProject.SavePath) && string.IsNullOrEmpty(path))
            return false;

        if (!string.IsNullOrEmpty(path))
        {
            savePath = Path.GetDirectoryName(path) ?? Directory.GetDirectoryRoot(path);
            projectFileName = Path.GetFileName(path);
        }
        else
        {
            savePath = ActiveProject.SavePath!;
            projectFileName = ActiveProject.ProjectFileName;
        }

        YAMLWrapper.Serialize(Path.Join(savePath, projectFileName), ActiveProject);

        if (ActiveProject.Stages.Count > 0)
            StageHandler.SaveProjectStages(savePath, ActiveProject.Stages);

        // TO-DO: Objects.

        return true;
    }

    /// <summary>
    /// Unloads the current project. The project should be saved before calling this method.
    /// </summary>
    public static void UnloadProject()
    {
        Debug.Assert(ActiveProject.Saved);

        ActiveProject = new();
        ProjectLoaded = false;
    }

    /// <param name="path">The path to the project file.</param>
    public static void CreateNew(string path, string name = "Untitled")
    {
        string dir = Path.GetDirectoryName(path) ?? Directory.GetDirectoryRoot(path);

        ActiveProject = new()
        {
            Name = name,
            SavePath = dir,
            ProjectFileName = Path.GetFileName(path)
        };

        YAMLWrapper.Serialize(path, ActiveProject);

        ProjectLoaded = true;
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
