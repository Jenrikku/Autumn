using System.Numerics;
using Autumn.GUI.Windows;
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
    private bool _WASD = false;
    private bool _MiddleMovesCamera = false;

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
        _WASD = _window.ContextHandler.SystemSettings.UseWASD;
        _MiddleMovesCamera = _window.ContextHandler.SystemSettings.UseMiddleMouse;
        _mouseSpeed = _window.ContextHandler.SystemSettings.MouseSpeed;
        _oldStyle = _window.ContextHandler.SystemSettings.Theme;
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

        ImGui.OpenPopup("Settings");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new(0.5f, 0.5f));

        if (
            !ImGui.BeginPopupModal(
                "Settings",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        #region Styling

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

        ImGui.Checkbox("Use ClassNames", ref _useClassNames);

        ImGui.Separator();

        ImGui.Checkbox("Use WASD to move the viewport camera", ref _WASD);
        ImGui.SetItemTooltip("Please be aware that his WILL interfere with other editor commands.");
        ImGui.Checkbox("Use middle click instead of right click to move the camera", ref _MiddleMovesCamera);

        ImGui.Separator();

        ImGui.InputInt("Camera Speed", ref _mouseSpeed, 1, default);
        ImGui.SetItemTooltip("Recommended values: 20, 35");
        _mouseSpeed = int.Clamp(_mouseSpeed, 10, 120);

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
        ImGui.SetCursorPosX(dimensions.X / 2);

        if (ImGui.Button("Reset", new(80, 0)))
        {
            _window.ContextHandler.SetProjectSetting("UseClassNames", false);
            _window.ContextHandler.SystemSettings.UseWASD = false;
            _window.ContextHandler.SystemSettings.UseMiddleMouse = false;
            _window.ContextHandler.SystemSettings.Theme = default;
            _window.ContextHandler.SystemSettings.MouseSpeed = default;
            ImGui.StyleColorsDark();
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            _isOpened = false;
            Reset();
            return;
        }

        ImGui.SetItemTooltip("This will set all values to their default.");

        ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - 10 - 80);

        if (ImGui.Button("Ok", new(80, 0)))
        {
            _window.ContextHandler.SetProjectSetting("UseClassNames", _useClassNames);
            _window.ContextHandler.SystemSettings.UseWASD = _WASD;
            _window.ContextHandler.SystemSettings.UseMiddleMouse = _MiddleMovesCamera;
            _window.ContextHandler.SystemSettings.Theme = _selectedStyle;
            _window.ContextHandler.SystemSettings.MouseSpeed = _mouseSpeed;
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
