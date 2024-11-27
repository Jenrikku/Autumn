using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.Wrappers;

namespace Autumn.Context;

internal class ContextHandler
{
    private const string mainConfFile = "config.yml";
    private const string systemConfFile = "system.yml";
    private const string actionsConfFile = "actions.yml";

    public LayeredSettings Settings { get; private set; }
    public SystemSettings SystemSettings { get; }
    public string SettingsPath { get; }

    public LayeredFSHandler FSHandler { get; }
    public ActionHandler ActionHandler { get; }

    public SortedSet<(string Name, byte Scenario)> ProjectStages { get; } = new();

    private Dictionary<string, object?> _globalSettings;
    private Project? _project;

    public bool IsProjectLoaded => _project is not null;

    public ContextHandler(string settingsDir)
    {
        SettingsPath = settingsDir;
        string settingsFile = Path.Join(settingsDir, mainConfFile);
        string sysSettingsFile = Path.Join(settingsDir, systemConfFile);
        string actionsFile = Path.Join(settingsDir, actionsConfFile);

        _globalSettings =
            YAMLWrapper.Deserialize<Dictionary<string, object?>>(settingsFile) ?? new();

        Settings = new(_globalSettings);

        if (Settings.RomFSPath is not null)
            FSHandler = new(Settings.RomFSPath);
        else
            FSHandler = new();

        SystemSettings = YAMLWrapper.Deserialize<SystemSettings>(sysSettingsFile) ?? new();

        var actions =
            YAMLWrapper.Deserialize<Dictionary<CommandID, Shortcut>>(actionsFile) ?? new();

        ActionHandler = new(actions);
    }

    /// <summary>
    /// Creates a new project based on the given directory.
    /// </summary>
    /// <param name="projectDir">The directory of the project.</param>
    public void NewProject(string projectDir)
    {
        Project project = new(projectDir);

        Directory.CreateDirectory(project.ContentsPath);
        _project = project;

        Settings = new(project.ProjectSettings, _globalSettings);

        if (Settings.RomFSPath is not null)
            FSHandler.SetPaths(project.ContentsPath, Settings.RomFSPath);
        else
            FSHandler.SetPaths(project.ContentsPath);

        SystemSettings.AddRecentlyOpenedPath(projectDir);
        SaveSettings();
    }

    /// <summary>
    /// Opens a project from the given directory.
    /// </summary>
    /// <param name="projectDir">The directory of the project.</param>
    /// <returns>Whether the project was opened correctly.</returns>
    public bool OpenProject(string projectDir)
    {
        Project project = new(projectDir);
        string projectFile = project.ProjectFile;

        if (!File.Exists(projectFile))
            return false;

        var projectSettings = YAMLWrapper.Deserialize<Dictionary<string, object?>>(projectFile);

        if (projectSettings is null)
            return false;

        project.ProjectSettings = projectSettings;
        _project = project;

        Settings = new(projectSettings, _globalSettings);

        if (Settings.RomFSPath is not null)
            FSHandler.SetPaths(project.ContentsPath, Settings.RomFSPath);
        else
            FSHandler.SetPaths(project.ContentsPath);

        SystemSettings.AddRecentlyOpenedPath(projectDir);
        SaveSettings();

        UpdateProjectStages();

        return true;
    }

    /// <summary>
    /// Checks the disk for changes on the project stages.
    /// </summary>
    public void UpdateProjectStages()
    {
        if (!Directory.Exists(_project?.ContentsPath))
            return;

        ProjectStages.Clear();

        RomFSHandler fSHandler = new(_project.ContentsPath);

        foreach (var (name, scenario) in fSHandler.EnumerateStages())
            ProjectStages.Add((name, scenario));
    }

    public void SetGlobalSetting(string key, object? value)
    {
        if (!_globalSettings.TryAdd(key, value))
            _globalSettings[key] = value;
    }

    public void SetProjectSetting(string key, object? value)
    {
        if (_project is null)
            return;

        Dictionary<string, object?> settings = _project.ProjectSettings;

        if (!settings.TryAdd(key, value))
            settings[key] = value;
    }

    /// <summary>
    /// Saves both project and global settings to the disk.
    /// </summary>
    public void SaveSettings()
    {
        if (_project is not null)
            YAMLWrapper.Serialize(_project.ProjectFile, _project.ProjectSettings);

        YAMLWrapper.Serialize(Path.Join(SettingsPath, mainConfFile), _globalSettings);

        YAMLWrapper.Serialize(Path.Join(SettingsPath, systemConfFile), SystemSettings);

        // Save actions:

        Dictionary<CommandID, Shortcut> actions = new();

        foreach (CommandID commandID in Enum.GetValues<CommandID>())
        {
            var (_, shortcut) = ActionHandler.GetAction(commandID);

            if (shortcut is null)
                continue;

            actions.Add(commandID, shortcut);
        }

        YAMLWrapper.Serialize(Path.Join(SettingsPath, actionsConfFile), actions);
    }
}