using System.Numerics;
using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.GUI.Windows;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

internal class ShortcutsDialog(MainWindowContext window)
{
    private bool _isOpened = false;

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("Autumn Shortcuts");

        Vector2 dimensions = new(450 * window.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Autumn Shortcuts",
                ref _isOpened,
                ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;
        ImGui.SeparatorText("General Shortcuts");
        ShortcutText(CommandID.NewProject);
        ShortcutText(CommandID.OpenProject);
        ShortcutText(CommandID.AddStage);
        ShortcutText(CommandID.OpenSettings);
        ShortcutText(CommandID.AddObject);
        ShortcutText(CommandID.Undo);
        ShortcutText(CommandID.Redo);
        ImGui.SeparatorText("Selection Shortcuts");
        ShortcutText(CommandID.HideObj);
        ShortcutText(CommandID.DuplicateObj);
        ShortcutText(CommandID.RemoveObj);
        ImGui.SeparatorText("Viewport Shortcuts");
        ImGui.Spacing();


        ImGui.EndPopup();
    }
    
    void ShortcutText(CommandID command)
    {
        var act = window.ContextHandler.ActionHandler.GetAction(command);
        if (act.Command != null)
        {
            ImGui.BulletText(act.Command.DisplayName);
            ImGui.SameLine();
        }
        if (act.Shortcut != null)
        {
            ImGui.Text("- " + act.Shortcut.DisplayString);
        }
        else
            ImGui.Text("- No shortcut assigned.");

    }
}
