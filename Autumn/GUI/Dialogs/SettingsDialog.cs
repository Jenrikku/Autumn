using System.Numerics;
using Autumn.GUI.Theming;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// Dialog that lets the user tweak the project and editor settings.
/// </summary>
internal class SettingsDialog
{
    private readonly MainWindowContext _window;

    private bool _isOpened = false;
    private bool _useClassNames = false;
    private bool _dbEditor = false;
    private bool _rememberLayout = false;
    private bool _prevlightonload = false;
    private bool _wasd = false;
    private bool _middleMovesCamera = false;
    private bool _zoomToMouse = false;
    private bool _enableVSync = true;
    private bool _restoreNativeFileDialogs = false;
    private int _compLevel = 1;
    private bool _loadLast = true;
    private bool[] _visibleDefaults = [false, true, true, true, false]; // Areas, CameraAreas, Rails, Grid, Transparentwall
    private bool _viewrelationLine = true;
    private int _hoverInfo = 0;

    private string[] compressionLevels = Enum.GetNames(typeof(Yaz0Wrapper.CompressionLevel));
    private int _oldTheme = 0;
    private int _selectedTheme = 0;
    private int _mouseSpeed = 20;
    private string _romfspath = "";
    private bool _romfsIsValidPath = true;
    Vector2 dimensions = new(450, 0);

    private readonly List<string> _availableThemes = new();

    /// <summary>
    /// Whether to reset the dialog to its defaults values once the "Ok" button has been pressed.
    /// </summary>

    public SettingsDialog(MainWindowContext window)
    {
        Reset();
        dimensions *= window.ScalingFactor;
        _window = window;
        _useClassNames = window.ContextHandler.Settings.UseClassNames;
    }

    public void Open()
    {
        _isOpened = true;
        _useClassNames = _window.ContextHandler.Settings.UseClassNames;
        _wasd = _window.ContextHandler.SystemSettings.UseWASD;
        _dbEditor = _window.ContextHandler.SystemSettings.EnableDBEditor;
        _middleMovesCamera = _window.ContextHandler.SystemSettings.UseMiddleMouse;
        _mouseSpeed = _window.ContextHandler.SystemSettings.MouseSpeed;
        _zoomToMouse = _window.ContextHandler.SystemSettings.ZoomToMouse;
        _enableVSync = _window.ContextHandler.SystemSettings.EnableVSync;
        _restoreNativeFileDialogs = _window.ContextHandler.SystemSettings.RestoreNativeFileDialogs;
        _compLevel = Array.IndexOf(Enum.GetValues<Yaz0Wrapper.CompressionLevel>(), _window.ContextHandler.SystemSettings.Yaz0Compression);
        _window.ContextHandler.SystemSettings.VisibleDefaults.CopyTo(_visibleDefaults, 0);
        _loadLast = _window.ContextHandler.SystemSettings.OpenLastProject;
        _rememberLayout = _window.ContextHandler.SystemSettings.RememberLayout;
        _romfspath = _window.ContextHandler.Settings.RomFSPath ?? "";
        _romfsIsValidPath = Directory.Exists(_romfspath);
        _prevlightonload = _window.ContextHandler.SystemSettings.AlwaysPreviewStageLights;
        _viewrelationLine = _window.ContextHandler.SystemSettings.ShowRelationLines;
        _hoverInfo = (int)_window.ContextHandler.SystemSettings.ShowHoverInfo;

        ReloadThemes();
    }

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        _useClassNames = false;
        _dbEditor = false;
        _wasd = false;
        _middleMovesCamera = false;
        _enableVSync = true;
        _restoreNativeFileDialogs = false;
        _compLevel = 1;
        _oldTheme = 0;
        _selectedTheme = 0;
        _mouseSpeed = 20;
    }

    public void ReloadThemes()
    {
        _availableThemes.Clear();
        _availableThemes.AddRange(ThemeLoader.EnumerateAllThemeNames(_window.ContextHandler.SettingsPath));

        _selectedTheme = _availableThemes.IndexOf(_window.ContextHandler.SystemSettings.Theme);
        if (_selectedTheme < 0) _selectedTheme = 0;

        _oldTheme = _selectedTheme;
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Reset();
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("Settings");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f * _window.ScalingFactor, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Settings",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        if (ImGui.BeginTabBar("##stngtabs"))
        {
            if (ImGui.BeginTabItem("UI"))
            {
                #region Styling

                if (_availableThemes.Count <= 0) ImGui.BeginDisabled();

                if (ImGui.Combo("Theme", ref _selectedTheme, _availableThemes.ToArray(), _availableThemes.Count))
                {
                    Theme? theme = ThemeLoader.LoadThemeByName(_availableThemes[_selectedTheme], _window.ContextHandler.SettingsPath);
                    if (theme is not null) _window.WindowManager.GlobalTheme = theme;
                }

                if (_availableThemes.Count <= 0) ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGui.Button(IconUtils.FOLDER))
                {
                    SingleFileChooserContext fileChooser = new(_window.ContextHandler, _window.WindowManager);
                    fileChooser.SuccessCallback += result =>
                    {
                        if (!ThemeLoader.IsValidTomlFile(result[0])) return;

                        string themeDir = Path.Join(_window.ContextHandler.SettingsPath, ThemeLoader.UserThemeSuffix);
                        string dest = Path.Join(themeDir, Path.GetFileNameWithoutExtension(result[0])) + ".toml";
                        Directory.CreateDirectory(themeDir);
                        File.Copy(result[0], dest, true);

                        ReloadThemes();
                    };

                    _window.WindowManager.Add(fileChooser);
                }

                ImGui.SameLine();

                bool isReadOnly = ThemeLoader.IsThemeReadOnly(_availableThemes[_selectedTheme], _window.ContextHandler.SettingsPath);

                if (isReadOnly) ImGui.BeginDisabled();

                if (ImGui.Button(IconUtils.TRASH))
                {
                    string? themePath = ThemeLoader.GetThemeFullPathByName(_availableThemes[_selectedTheme], _window.ContextHandler.SettingsPath);
                    if (themePath is not null)
                        File.Delete(themePath);

                    ReloadThemes();

                    Theme? theme = ThemeLoader.LoadThemeByName(_availableThemes[_selectedTheme], _window.ContextHandler.SettingsPath);
                    if (theme is not null) _window.WindowManager.GlobalTheme = theme;
                }

                if (isReadOnly) ImGui.EndDisabled();

                #endregion

                ImGui.Checkbox("Remember layout", ref _rememberLayout);
                ImGui.SameLine();
                ImGuiWidgets.HelpTooltip("Will save the window positions and sizes when closing the program");
                ImGui.EndTabItem();
            }


            if (ImGui.BeginTabItem("Viewport"))
            {
                ImGui.Checkbox("Use WASD to move the viewport camera", ref _wasd);
                ImGui.SameLine();
                ImGuiWidgets.HelpTooltip("Please be aware that this WILL interfere with other editor commands for now.");

                ImGui.Checkbox("Use middle click instead of right click to move the camera", ref _middleMovesCamera);
                ImGui.Checkbox("Zoom to mouse", ref _zoomToMouse);
                ImGui.InputInt("Camera Speed", ref _mouseSpeed, 1, default);
                ImGui.SameLine();
                ImGuiWidgets.HelpTooltip("Recommended values: 20, 35");
                _mouseSpeed = int.Clamp(_mouseSpeed, 10, 120);
                ImGui.Combo("Hover info", ref _hoverInfo, ["Disabled", "Tooltip", "Highlight (not implemented)", "Status (not implemented)"], 4);
                ImGui.Checkbox("Preview stage lights without opening the ligths window", ref _prevlightonload); 
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Editor Functionality"))
            {
                ImGui.Checkbox("Use ClassNames", ref _useClassNames);
                ImGui.Checkbox("Enable Database Editor", ref _dbEditor);
                ImGui.Checkbox("Restore Native File Dialogs", ref _restoreNativeFileDialogs);
                ImGui.Checkbox("Enable VSync", ref _enableVSync);
                ImGui.SameLine();
                ImGuiWidgets.HelpTooltip("This option requires restarting the editor");
                ImGui.Combo("Yaz0 Compression Level", ref _compLevel, compressionLevels, 5);
                string rfs = _romfspath;
                if (ImGui.Button(IconUtils.FOLDER))
                {
                    if (!_window!.ContextHandler.SystemSettings.RestoreNativeFileDialogs)
                    {
                        ProjectChooserContext projectChooser = new(_window.ContextHandler, _window.WindowManager);
                        projectChooser.Title = "Autumn: Select Base ROMFS folder";
                        _window.WindowManager.Add(projectChooser);

                        projectChooser.SuccessCallback += result =>
                        {
                            _romfspath = result[0];
                        };
                    }
                    else
                    {
                        if (TinyFileDialogs.SelectFolderDialog(out string? dialogOutput, "Select the folder containing the RomFS"))
                        {
                            _romfspath = dialogOutput;
                        }
                    }
                }
                ImGui.SameLine(default, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGuiWidgets.InputTextRedWhenInvalid("Base ROMFS Path", ref rfs, 128, !_romfsIsValidPath, "Path to ROMFS folder");
                if (rfs != _romfspath)
                {
                    _romfspath = rfs;
                    _romfsIsValidPath = Directory.Exists(_romfspath);
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Defaults"))
            {
                ImGui.Checkbox("Load last project on launch", ref _loadLast);
                ImGui.Text("Visibility:");
                ImGui.Checkbox("Show Areas", ref _visibleDefaults[0]);
                ImGui.Checkbox("Show CameraAreas", ref _visibleDefaults[1]);
                ImGui.Checkbox("Show Rails", ref _visibleDefaults[2]);
                ImGui.Checkbox("Show Grid", ref _visibleDefaults[3]);
                ImGui.Checkbox("Show Transparentwalls", ref _visibleDefaults[4]);
                ImGui.Checkbox("Show Relationship Lines", ref _viewrelationLine);
                ImGui.SameLine();
                ImGuiWidgets.HelpTooltip("Shows a line between child objects and their parents");
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.SeparatorText("Reset");

        float resetWidth = ImGui.GetContentRegionAvail().X / 3;
        if (ImGui.Button("Values", new(resetWidth - ImGui.GetStyle().ItemInnerSpacing.X, 0)))
        {
            _window.ContextHandler.SetProjectSetting("UseClassNames", false);
            _window.ContextHandler.SystemSettings.Reset();
            Yaz0Wrapper.Level = _window.ContextHandler.SystemSettings.Yaz0Compression;

            if (_availableThemes.Count > 0)
            {
                Theme? theme = ThemeLoader.LoadThemeByName(_availableThemes[_oldTheme], _window.ContextHandler.SettingsPath);
                if (theme is not null) _window.WindowManager.GlobalTheme = theme;
            }

            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }
        ImGui.SetItemTooltip("This will set all values to their default.");
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);

        if (ImGui.Button("Shortcuts", new(resetWidth - ImGui.GetStyle().ItemInnerSpacing.X, 0)))
        {
            File.Copy(Path.Join("Resources", "DefaultActions.yml"), Path.Join(_window.ContextHandler.SettingsPath, "actions.yml"), true);
            _window.ContextHandler.LoadActions(null);
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);

        if (ImGui.Button("Layout", new(resetWidth, 0)))
        {
            File.Copy(Path.Join("Resources", "DefaultLayout.ini"), Path.Join(_window.ContextHandler.SettingsPath, "imgui.ini"), true);
            ImGui.LoadIniSettingsFromDisk(Path.Join("Resources", "DefaultLayout.ini"));
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            _window.ContextHandler.SystemSettings.RememberLayout = false;
            Reset();
            return;
        }

        // ImGui.TextColored(new Vector4(1, 0, 0, 1), "Test error text");

        if (ImGui.Button("Cancel", new(80, 0)))
        {
            if (_availableThemes.Count > 0)
            {
                Theme? theme = ThemeLoader.LoadThemeByName(_availableThemes[_oldTheme], _window.ContextHandler.SettingsPath);
                if (theme is not null) _window.WindowManager.GlobalTheme = theme;
            }

            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - ImGui.GetStyle().WindowPadding.X - 80);

        if (ImGui.Button("Ok", new(80, 0)))
        {
            _window.ContextHandler.SetGlobalSetting("RomFSPath", _romfspath);
            _window.ContextHandler.SetProjectSetting("UseClassNames", _useClassNames);
            _window.ContextHandler.SystemSettings.UseWASD = _wasd;
            _window.ContextHandler.SystemSettings.UseMiddleMouse = _middleMovesCamera;
            _window.ContextHandler.SystemSettings.EnableVSync = _enableVSync;
            _window.ContextHandler.SystemSettings.MouseSpeed = _mouseSpeed;
            _window.ContextHandler.SystemSettings.ZoomToMouse = _zoomToMouse;
            _window.ContextHandler.SystemSettings.EnableDBEditor = _dbEditor;
            _window.ContextHandler.SystemSettings.RestoreNativeFileDialogs = _restoreNativeFileDialogs;
            _window.ContextHandler.SystemSettings.Yaz0Compression = Enum.GetValues<Yaz0Wrapper.CompressionLevel>()[_compLevel];
            Yaz0Wrapper.Level = _window.ContextHandler.SystemSettings.Yaz0Compression;
            _visibleDefaults.CopyTo(_window.ContextHandler.SystemSettings.VisibleDefaults, 0);
            _window.ContextHandler.SystemSettings.OpenLastProject = _loadLast;
            _window.ContextHandler.SystemSettings.RememberLayout = _rememberLayout;
            _window.ContextHandler.SystemSettings.AlwaysPreviewStageLights = _prevlightonload;
            _window.ContextHandler.SystemSettings.ShowRelationLines = _viewrelationLine;
            _window.ContextHandler.SystemSettings.ShowHoverInfo = (Enums.HoverInfoMode)_hoverInfo;

            if (_availableThemes.Count > 0)
                _window.ContextHandler.SystemSettings.Theme = _availableThemes[_selectedTheme];
            
            ModelRenderer.VisibleAreas = _visibleDefaults[0];
            ModelRenderer.VisibleCameraAreas = _visibleDefaults[1];
            ModelRenderer.VisibleRails = _visibleDefaults[2];
            ModelRenderer.VisibleGrid = _visibleDefaults[3];
            ModelRenderer.VisibleTransparentWall = _visibleDefaults[4];
            ModelRenderer.VisibleRelationLines = _viewrelationLine;

            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _window.ContextHandler.SaveSettings();
            _isOpened = false;
            Reset();
            return;
        }

        ImGui.EndPopup();
    }
}
