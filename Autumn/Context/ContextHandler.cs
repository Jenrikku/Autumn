using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn.Context;

internal class ContextHandler
{
    private const string mainConfFile = "config.yml";
    private const string systemConfFile = "system.yml";
    private const string actionsConfFile = "actions.yml";

    public bool ProjectChanged { get; set; } = false;
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

        _globalSettings = YAMLWrapper.Deserialize<Dictionary<string, object?>>(settingsFile) ?? new();

        Settings = new(_globalSettings);
        SystemSettings = YAMLWrapper.Deserialize<SystemSettings>(sysSettingsFile) ?? new();

        FSHandler = new(Settings.RomFSPath);

        var actions = YAMLWrapper.Deserialize<Dictionary<string, object>>(actionsFile);
        actions ??= YAMLWrapper.Deserialize<Dictionary<string, object>>(Path.Join("Resources", "DefaultActions.yml"));

        // Convert to Dictionary<CommandID, Shortcut>

        Dictionary<CommandID, Shortcut> parsedActions = new();

        foreach (var (key, value) in actions ?? [])
        {
            if (!Enum.TryParse(key, out CommandID id))
                continue;

            if (value is not Dictionary<object, object> dict)
                continue;

            bool ctrl = false;
            bool shift = false;
            bool alt = false;
            ImGuiKey imGuiKey = ImGuiKey.None;

            dict.TryGetValue("Ctrl", out object? ctrlVal);
            dict.TryGetValue("Shift", out object? shiftVal);
            dict.TryGetValue("Alt", out object? altVal);
            dict.TryGetValue("Key", out object? imGuiKeyVal);

            if (ctrlVal is bool v0)
                ctrl = v0;

            if (shiftVal is bool v1)
                shift = v1;

            if (altVal is bool v2)
                alt = v2;

            if (imGuiKeyVal is string v3 && Enum.TryParse(v3, out ImGuiKey v4))
                imGuiKey = v4;

            parsedActions.Add(id, new(ctrl, shift, alt, imGuiKey));
        }

        ActionHandler = new(parsedActions);
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

        FSHandler.ModFS = string.IsNullOrEmpty(project.ContentsPath) ? null : new(project.ContentsPath);

        SystemSettings.AddRecentlyOpenedPath(projectDir);
        SaveSettings();
        ProjectChanged = true;
    }

    /// <summary>
    /// Opens a project from the given directory.
    /// </summary>
    /// <param name="projectDir">The directory of the project.</param>
    /// <returns>Whether the project was opened correctly.</returns>
    public bool OpenProject(string projectDir)
    {
        if (!Directory.Exists(projectDir))
            return false;

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

        FSHandler.ModFS = string.IsNullOrEmpty(project.ContentsPath) ? null : new(project.ContentsPath);
        SystemSettings.AddRecentlyOpenedPath(projectDir);
        SaveSettings();

        UpdateProjectStages();
        ProjectChanged = true;

        return true;
    }

    /// <summary>
    /// Checks the disk for changes on the project stages.
    /// </summary>
    public void UpdateProjectStages()
    {
        if (FSHandler.ModFS is null)
            return;

        ProjectStages.Clear();

        foreach (var (name, scenario) in FSHandler.ModFS.EnumerateStages())
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

    public void SetCurrentProjectAsLastOpened() =>
        SystemSettings.LastOpenedProject = _project?.SavePath ?? string.Empty;
}
