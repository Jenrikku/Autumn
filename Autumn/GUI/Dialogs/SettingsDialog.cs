using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Wrappers;
using ImGuiNET;

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
    private bool _wasd = false;
    private bool _middleMovesCamera = false;
    private bool _enableVSync = false;
    private int _compLevel = 1;

    private string[] compressionLevels = Enum.GetNames(typeof(Yaz0Wrapper.CompressionLevel));
    private int _oldStyle = 0;
    private int _selectedStyle = 0;
    private int _mouseSpeed = 20;
    Vector2 dimensions = new(450, 0);

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
        _selectedStyle = _window.ContextHandler.SystemSettings.Theme;
        _wasd = _window.ContextHandler.SystemSettings.UseWASD;
        _dbEditor = _window.ContextHandler.SystemSettings.EnableDBEditor;
        _middleMovesCamera = _window.ContextHandler.SystemSettings.UseMiddleMouse;
        _mouseSpeed = _window.ContextHandler.SystemSettings.MouseSpeed;
        _oldStyle = _window.ContextHandler.SystemSettings.Theme;
        _enableVSync = _window.ContextHandler.SystemSettings.EnableVSync;
        _compLevel = Array.IndexOf(Enum.GetValues<Yaz0Wrapper.CompressionLevel>(), _window.ContextHandler.SystemSettings.Yaz0Compression);

    }

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        _useClassNames = false;
        _selectedStyle = 0;
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

        #region Styling

        ImGui.SeparatorText("UI settings");
        string[] comb = ["Default", "Dark", "Light"];
        ImGui.Combo("Application style", ref _selectedStyle, comb, comb.Count());

        switch (_selectedStyle)
        {
            default:
                ImGui.StyleColorsDark();
                break;

            case 2:
                ImGui.StyleColorsLight();
                break;
        }

        #endregion

        ImGui.Checkbox("Enable VSync", ref _enableVSync);
        ImGui.SameLine();
        ImGuiWidgets.HelpTooltip("This option requires restarting the editor");

        ImGui.SeparatorText("Viewport");

        ImGui.Checkbox("Use WASD to move the viewport camera", ref _wasd);
        ImGui.SameLine();
        ImGuiWidgets.HelpTooltip("Please be aware that this WILL interfere with other editor commands.");

        ImGui.Checkbox("Use middle click instead of right click to move the camera", ref _middleMovesCamera);
        ImGui.InputInt("Camera Speed", ref _mouseSpeed, 1, default);
        ImGui.SameLine();
        ImGuiWidgets.HelpTooltip("Recommended values: 20, 35");
        _mouseSpeed = int.Clamp(_mouseSpeed, 10, 120);

        ImGui.SeparatorText("Editor Functionality");

        ImGui.Checkbox("Use ClassNames", ref _useClassNames);
        ImGui.Checkbox("Enable Database Editor", ref _dbEditor);
        ImGui.Combo("Yaz0 Compression Level", ref _compLevel, compressionLevels, 5);

        ImGui.SeparatorText("Reset");

        float resetWidth = ImGui.GetWindowWidth() / 2 - ImGui.GetStyle().ItemSpacing.X*1.65f;
        if (ImGui.Button("Values", new(resetWidth,0)))
        {
            _window.ContextHandler.SetProjectSetting("UseClassNames", false);
            _window.ContextHandler.SystemSettings.UseWASD = false;
            _window.ContextHandler.SystemSettings.UseMiddleMouse = false;
            _window.ContextHandler.SystemSettings.EnableVSync = true;
            _window.ContextHandler.SystemSettings.Theme = 0;
            _window.ContextHandler.SystemSettings.MouseSpeed = 20;
            _window.ContextHandler.SystemSettings.EnableDBEditor = false;
            _window.ContextHandler.SystemSettings.Yaz0Compression = Yaz0Wrapper.CompressionLevel.Medium;
            Yaz0Wrapper.Level = _window.ContextHandler.SystemSettings.Yaz0Compression;
            
            ImGui.StyleColorsDark();
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }
        ImGui.SetItemTooltip("This will set all values to their default.");
        ImGui.SameLine();

        if (ImGui.Button("Layout", new(resetWidth,0)))
        {
            string stpth = Path.Join(_window.ContextHandler.SettingsPath, "imgui.ini");
            if (File.Exists(stpth))
                File.Delete(stpth);
            File.Copy(Path.Join("Resources", "DefaultLayout.ini"), Path.Join(_window.ContextHandler.SettingsPath, "imgui.ini"));
            ImGui.LoadIniSettingsFromDisk(Path.Join("Resources", "DefaultLayout.ini"));
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }

        // ImGui.TextColored(new Vector4(1, 0, 0, 1), "Test error text");

        if (ImGui.Button("Cancel", new(80, 0)))
        {
            switch (_oldStyle)
            {
                default:
                    ImGui.StyleColorsDark();
                    break;

                case 2:
                    ImGui.StyleColorsLight();
                    break;
            }

            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - 10 - 80);

        if (ImGui.Button("Ok", new(80, 0)))
        {
            _window.ContextHandler.SetProjectSetting("UseClassNames", _useClassNames);
            _window.ContextHandler.SystemSettings.UseWASD = _wasd;
            _window.ContextHandler.SystemSettings.UseMiddleMouse = _middleMovesCamera;
            _window.ContextHandler.SystemSettings.EnableVSync = _enableVSync;
            _window.ContextHandler.SystemSettings.Theme = _selectedStyle;
            _window.ContextHandler.SystemSettings.MouseSpeed = _mouseSpeed;
            _window.ContextHandler.SystemSettings.EnableDBEditor = _dbEditor;
            _window.ContextHandler.SystemSettings.Yaz0Compression = Enum.GetValues<Yaz0Wrapper.CompressionLevel>()[_compLevel];
            Yaz0Wrapper.Level = _window.ContextHandler.SystemSettings.Yaz0Compression;
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
