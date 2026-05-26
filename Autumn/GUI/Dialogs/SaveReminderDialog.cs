using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A popup that prompts the user to save when the stage is about to be closed.
/// </summary>
internal class SaveReminderDialog(MainWindowContext window)
{
    private bool _isOpened = false;
    public bool IsOpen => _isOpened;
    Scene _closingScene;

    public void Open(Scene closingScene)
    {
        _closingScene = closingScene;
        _isOpened = true;
    }
    public void Render()
    {
        if (!_isOpened || _closingScene is null)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("Warning!##SaveReminder");

        Vector2 dimensions = new(500 * window.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Appearing);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Warning!##SaveReminder",
                ref _isOpened,
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize
            )
        )
            return;

        ImGui.TextWrapped(
            "The stage seems to have been modified, do you want to save before closing?"
        );

        ImGui.Spacing();

        Vector2 buttonSize = new(50 * window.ScalingFactor, 0);
        if (ImGui.Button("Cancel", buttonSize))
        {
            ImGui.CloseCurrentPopup();
            _isOpened = false;
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - buttonSize.X * 3.3f);
        if (ImGui.Button("Close without saving"))
        {
            ImGui.CloseCurrentPopup();
            _closingScene.IsSaved = true;
            _isOpened = false;
            window.CloseStage(_closingScene);
        }
        ImGui.SameLine();
        if (ImGui.Button("Save and close"))
        {
            ImGui.CloseCurrentPopup();
            window.BackgroundManager.Add(
                "Saving...",
                manager =>
                {
                    Scene scene = _closingScene!;
                    Stage stage = _closingScene!.Stage!;
                    window.ContextHandler.FSHandler.WriteStage(stage, window.ContextHandler.Settings.UseClassNames);
                    scene.SaveUndoCount = _closingScene.History.UndoSteps;
                    if (!window.ContextHandler.ProjectStages.Contains(new(stage.Name, stage.Scenario)))
                    {
                        window.ContextHandler.AddProjectStage(stage.Name, stage.Scenario);
                    }
                });
            _closingScene.IsSaved = true;
            window.CloseStage(_closingScene);
            _isOpened = false;
        }


        ImGui.EndPopup();
    }
}
