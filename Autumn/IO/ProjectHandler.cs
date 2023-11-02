using System.Diagnostics;
using System.Text.RegularExpressions;
using Autumn.Storage;

namespace Autumn.IO;

internal static partial class ProjectHandler
{
    public const ushort SupportedVersion = 0;

    public static event Action? UnsupportedVersionEvent;

    public static bool ProjectLoaded { get; private set; } = false;

    public static Project ActiveProject;

    /// <param name="path">The path to the project file.</param>
    public static void LoadProject(string path)
    {
        ActiveProject = YAMLWrapper.Deserialize<Project>(path);

        if (ActiveProject.Version != SupportedVersion)
        {
            UnsupportedVersionEvent?.Invoke();
            return;
        }

        string dir = Path.GetDirectoryName(path) ?? Directory.GetDirectoryRoot(path);

        ActiveProject.SavePath = dir;
        ActiveProject.ProjectFileName = Path.GetFileName(path);

        string stagesDir = Path.Join(dir, "stages");
        Regex regex = StageFolderRegex();

        foreach (string stageDir in Directory.EnumerateDirectories(stagesDir))
        {
            string stageDirName = Path.GetFileName(stageDir)!;

            Match match = regex.Match(stageDirName);

            if (!match.Success)
                continue;

            string name = match.Groups[1].Value;
            byte scenario = byte.Parse(match.Groups[2].Value);

            ActiveProject.Stages.Add(new(name, scenario));
        }

        if (!RecentHandler.RecentOpenedPaths.Contains(path))
            RecentHandler.RecentOpenedPaths.Add(path);

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
        return true;
    }

    /// <summary>
    /// Unloads the current project. The project should be saved before calling this method.
    /// </summary>
    /// <param name="force">When this parameter is set to true, any unsaved contents are ignored.</param>
    public static void UnloadProject(bool force = false)
    {
        // TO-DO: Check whether stages are saved (!Loaded) before closing the project.
        // Suggestion: Handle stage saving somewhere else. Return false if project can't be unloaded because of stages.

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

        if (!RecentHandler.RecentOpenedPaths.Contains(path))
            RecentHandler.RecentOpenedPaths.Add(path);

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

    [GeneratedRegex("(.*)(\\d+\\b)")]
    private static partial Regex StageFolderRegex();
}
