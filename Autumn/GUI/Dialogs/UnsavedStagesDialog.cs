using System.Numerics;
using Autumn.Background;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Utils;
using Hexa.NET.ImGui;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that prompts the user to save or discard unsaved stages
/// before closing the current project.
/// Note: The constructor makes calls to ImGui methods.
/// </summary>
internal class UnsavedStagesDialog(MainWindowContext window)
{
    private bool _isOpened = false;
    public bool IsOpen => _isOpened;
    private List<Scene> _closingScenes = new();

    public void Open() 
    {
        _closingScenes.Clear();
        foreach (Scene scene in window.Scenes)
        {
            if (!scene.IsSaved) _closingScenes!.Add(scene);
        }
        _isOpened = true;
        _forceClose = false;
    }
    bool _forceClose = false;
    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("##UnsavedStagesDialog");

        Vector2 dimensions = new(450, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "##UnsavedStagesDialog",
                ref _isOpened,
                ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped(
            "It seems you still have some unsaved stages.\nPlease choose what to do with them:"
        );

        ImGui.Spacing();

        ImGuiStylePtr style = ImGui.GetStyle();
        Vector2 listSize = new(ImGui.GetContentRegionAvail().X,
                               ImGui.GetTextLineHeightWithSpacing() * 7.25f + style.FramePadding.Y * 2.0f);

        if (ImGui.BeginTable("##ClosingStageList", 3, ImGuiTableFlags.BordersOuter, listSize))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            
            int saveScene = -1;
            int removeScene = -1;

            for (int i = 0; i < _closingScenes.Count; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (ImGuiWidgets.HoverButton(IconUtils.FILE_ARROW_DOWN + $"##closingSave{i}"))
                {
                    saveScene = i;
                }
                ImGui.SetItemTooltip("Save changes");
                ImGui.TableSetColumnIndex(1);
                if (ImGuiWidgets.HoverButton(IconUtils.TRASH + $"##closingDestroy{i}"))
                {
                    removeScene = i;
                }
                ImGui.SetItemTooltip("Discard changes");
                ImGui.TableSetColumnIndex(2);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
                ImGui.Text(_closingScenes[i].DisplayName);
            }
            ImGui.EndTable();

            if (saveScene > -1)
            {
                Scene sc = _closingScenes[saveScene];
                SaveScene(sc);

                _closingScenes.RemoveAt(saveScene);
            }
            if (removeScene > -1)
            {
                _closingScenes.RemoveAt(removeScene);
            }
        }

        Vector2 buttonSize = new(70, 0);
        ImGui.SetCursorPosX(dimensions.X - buttonSize.X * 3 - style.ItemSpacing.X * 2 - ImGui.GetStyle().WindowPadding.X);

        bool wasforced = _forceClose;
        if (wasforced) ImGui.BeginDisabled();

        if (ImGui.Button("Save all", buttonSize))
        {
            for (int i = _closingScenes.Count - 1; i >= 0; i--)
            {
                Scene sc = _closingScenes[i];
                SaveScene(sc);
            }
            ForceClose();
            return;
            
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard all", buttonSize))
        {
            ForceClose();
            return;
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel", buttonSize))
        {
            Close();
            ImGui.EndPopup();
            return;
        }
        if (wasforced) ImGui.EndDisabled();

        // Once all stages have been saved, signal that the program can exit.
        if (_closingScenes.Count == 0 && !_forceClose)
        {
            ForceClose();
            return;
        }

        if (_forceClose && !window.BackgroundManager.IsBusy)
        {
            window.ForceClose = true;
            window.WindowManager.Stop();
        }


        ImGui.EndPopup();
    }

    private void Close()
    {
        _closingScenes.Clear();
        _isOpened = false;

        ImGui.CloseCurrentPopup();
    }

    private void ForceClose()
    {   
        ImGui.EndPopup();
        _forceClose = true;
    }

    #warning FIXME: Repeated code, find a better way to handle stage saving
    private void SaveScene(Scene sc)
    {
        window.BackgroundManager.Add(
            $"Saving {sc.DisplayName}...",
            manager =>
            {
                var stage = sc.Stage;
                window.ContextHandler.FSHandler.WriteStage(stage, window.ContextHandler.Settings.UseClassNames);
                sc.SaveUndoCount = sc.History.UndoSteps;
                if (!window.ContextHandler.ProjectStages.Contains(new(stage.Name, stage.Scenario)))
                {
                    window.ContextHandler.AddProjectStage(stage.Name, stage.Scenario);
                }
                sc.IsSaved = true;
            },
            priority: Enums.BackgroundTaskPriority.Highest
        );
    }
}
