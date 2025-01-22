using System.Numerics;
using Autumn.FileSystems;
using Autumn.Rendering;
using Autumn.Storage;
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

    private int _oldStyle = 0;
    private int _selectedStyle = 0;
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

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Settings",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        # region Styling

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
            _window.ContextHandler.SystemSettings.Theme = _selectedStyle;
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
