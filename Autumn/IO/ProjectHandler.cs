using System.Text.RegularExpressions;
using Autumn.Storage;
using Autumn.Wrappers;

namespace Autumn.IO;

internal static partial class ProjectHandler
{
    private static Project s_activeProject;

    /// <summary>
    /// The currently supported project version.
    /// </summary>
    public const ushort SupportedVersion = 0;

    /// <summary>
    /// An event that is triggered before a project with an unsupported version is loaded.<br />
    /// The parameter is the version of the project that wants to be loaded.
    /// </summary>
    public static event Action<ushort>? UnsupportedVersionEvent;

    /// <summary>
    /// An event that is triggered whenever the project wants to be closed.<br />
    /// The result value of the function indicates if the project can close safely.
    /// </summary>
    public static event Func<Project>? ProjectClosingEvent;

    /// <summary>
    /// An event that is triggered when a new project has been created.
    /// </summary>
    public static event Action? ProjectCreatedEvent;

    /// <summary>
    /// Whether there is a project loaded.
    /// </summary>
    public static bool ProjectLoaded { get; private set; } = false;

    public static List<Stage> Stages => s_activeProject.Stages;
    public static Dictionary<string, ActorObj> Objects => s_activeProject.Objects;
    public static SortedDictionary<string, object> Settings => s_activeProject.Settings;

    public static string? ProjectSavePath => s_activeProject.SavePath;

    public static string ProjectName
    {
        get => s_activeProject.Name;
        set => s_activeProject.Name = value;
    }

    public static string ProjectBuildOutput
    {
        get => s_activeProject.BuildOutput;
        set => s_activeProject.BuildOutput = value;
    }

    /// <summary>
    /// A property that tells whether the current project supports the ClassName stage object attribute.
    /// </summary>
    public static bool UseClassNames
    {
        get => SettingsHandler.GetValue<bool>("UseClassNames");
        set => Settings["UseClassNames"] = value;
    }

    /// <summary>
    /// Loads a project from the specified path.
    /// </summary>
    /// <param name="path">The path to the project file.</param>
    public static void LoadProject(string path)
    {
        if (!CloseProject())
            return;

        var project = YAMLWrapper.Deserialize<Project>(path);

        if (project.Version != SupportedVersion)
        {
            UnsupportedVersionEvent?.Invoke(project.Version);
            return;
        }

        s_activeProject = project;

        string dir = Path.GetDirectoryName(path) ?? Directory.GetDirectoryRoot(path);

        s_activeProject.SavePath = dir;
        s_activeProject.ProjectFileName = Path.GetFileName(path);

        string stagesDir = Path.Join(dir, "stages");
        Regex regex = StageFolderRegex();

        Directory.CreateDirectory(stagesDir);

        foreach (string stageDir in Directory.EnumerateDirectories(stagesDir))
        {
            string stageDirName = Path.GetFileName(stageDir)!;

            Match match = regex.Match(stageDirName);

            if (!match.Success)
                continue;

            string name = match.Groups[1].Value;
            byte scenario = byte.Parse(match.Groups[2].Value);

            s_activeProject.Stages.Add(new(name, scenario));
        }

        if (!RecentHandler.RecentOpenedPaths.Contains(path))
            RecentHandler.RecentOpenedPaths.Add(path);

        ProjectLoaded = true;
    }

    /// <summary>
    /// Saves the current project properties to the project file.<br />
    /// This does not save objects or stages.
    /// </summary>
    /// <returns>false if the project's save path is null or empty.</returns>
    public static bool SaveProjectFile()
    {
        if (string.IsNullOrEmpty(s_activeProject.SavePath))
            return false;

        string savePath = s_activeProject.SavePath!;
        string projectFileName = s_activeProject.ProjectFileName;

        YAMLWrapper.Serialize(Path.Join(savePath, projectFileName), s_activeProject);
        return true;
    }

    /// <summary>
    /// Triggers <see cref="ProjectClosingEvent"/> and closes the project if all the results were true.
    /// </summary>
    public static bool CloseProject()
    {
        if (ProjectClosingEvent is not null)
        {
            var invocationList = ProjectClosingEvent.GetInvocationList();

            foreach (Delegate func in invocationList)
            {
                object? result = func.DynamicInvoke();

                if (result is bool resBool && !resBool)
                    return false;
            }
        }

        s_activeProject = new();
        ProjectLoaded = false;
        return true;
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    /// <param name="path">The path to the project's directory.</param>
    public static void CreateNewProject(string path, string name = "Untitled")
    {
        if (!CloseProject())
            return;

        string filepath = Path.Join(path, "autumnproj.yml");
        string buildPath = Path.Join(path, "build");

        Directory.CreateDirectory(buildPath);

        s_activeProject = new()
        {
            Name = name,
            SavePath = path,
            BuildOutput = buildPath
        };

        YAMLWrapper.Serialize(filepath, s_activeProject);

        if (!RecentHandler.RecentOpenedPaths.Contains(filepath))
            RecentHandler.RecentOpenedPaths.Add(filepath);

        ProjectLoaded = true;
        ProjectCreatedEvent?.Invoke();
    }

    public static bool FileExists(string path)
    {
        if (!ProjectLoaded)
            return false;

        return File.Exists(Path.Join(s_activeProject.SavePath, path));
    }

    public static bool DirectoryExists(string path)
    {
        if (!ProjectLoaded)
            return false;

        return Directory.Exists(Path.Join(s_activeProject.SavePath, path));
    }

    [GeneratedRegex("(.*)(\\d+\\b)")]
    private static partial Regex StageFolderRegex();
}
