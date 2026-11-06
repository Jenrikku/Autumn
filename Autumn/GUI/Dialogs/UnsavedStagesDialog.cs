using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
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

    private OrderedDictionary<string, Scene>? _closingScenes;

    // Stores discarded stages.
    // If the user has discarded them but presses cancel, then they will be seen as unsaved again.
    private readonly HashSet<Scene> _discardedScenes = new();

    private ImGuiSelectionBasicStoragePtr _selection = ImGui.ImGuiSelectionBasicStorage();

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        if (_closingScenes is null)
        {
            _closingScenes = new();
            foreach (Scene scene in window.Scenes)
                if (!scene.IsSaved) _closingScenes.Add(scene.DisplayName, scene);
        }

        ImGui.OpenPopup("##UnsavedStagesDialog"u8);

        Vector2 dimensions = new(450, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "##UnsavedStagesDialog"u8,
                ref _isOpened,
                ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped(
            "It seems you still have some unsaved stages.\nPlease choose what to do with them:"u8
        );

        ImGui.Spacing();

        ImGuiStylePtr style = ImGui.GetStyle();
        Vector2 listSize = new(ImGui.GetContentRegionAvail().X,
                               ImGui.GetTextLineHeightWithSpacing() * 7.25f + style.FramePadding.Y * 2.0f);

        if (ImGui.BeginChild("##ClosingStageList", listSize, ImGuiChildFlags.FrameStyle))
        {
            var msIO = ImGui.BeginMultiSelect(ImGuiMultiSelectFlags.BoxSelect1D, _selection.Size, _closingScenes.Count);

            _selection.ApplyRequests(msIO);

            foreach (string key in _closingScenes.Keys)
            {
                uint hash = (uint)key.GetHashCode();

                bool selected = _selection.Contains(hash);
                ImGui.SetNextItemSelectionUserData(hash);
                ImGui.Selectable(key, selected);
            }

            msIO = ImGui.EndMultiSelect();
            _selection.ApplyRequests(msIO);
        }

        ImGui.EndChild();

        ImGui.TextDisabled("You may select more than one file at once"u8);

        Vector2 buttonSize = new(50, 0);
        ImGui.SetCursorPosX(dimensions.X - buttonSize.X * 3 - style.ItemSpacing.X * 2 - ImGui.GetStyle().WindowPadding.X);

        bool nothingSelected = _selection.Size == 0;

        if (nothingSelected) ImGui.BeginDisabled();

        if (ImGui.Button("Save"u8, buttonSize))
        {
            for (int i = 0; i < _closingScenes.Count; i++)
            {
                var (key, scene) = _closingScenes.GetAt(i);
                uint hash = (uint)key.GetHashCode();

                if (_selection.Contains(hash))
                {
                    // FIXME: Repeated code, find a better way to handle stage saving
                    window.BackgroundManager.Add(
                        $"Saving {key}...",
                        manager =>
                        {
                            var stage = scene.Stage;
                            window.ContextHandler.FSHandler.WriteStage(stage, window.ContextHandler.Settings.UseClassNames);
                            scene.SaveUndoCount = scene.History.UndoSteps;
                            if (!window.ContextHandler.ProjectStages.Contains(new(stage.Name, stage.Scenario)))
                            {
                                window.ContextHandler.AddProjectStage(stage.Name, stage.Scenario);
                            }
                        },
                        priority: Enums.BackgroundTaskPriority.Highest
                    );

                    scene.IsSaved = true;
                    _closingScenes.Remove(key);
                    i--;
                }
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"u8, buttonSize))
        {
            for (int i = 0; i < _closingScenes.Count; i++)
            {
                var (key, scene) = _closingScenes.GetAt(i);
                uint hash = (uint)key.GetHashCode();

                if (_selection.Contains(hash))
                {
                    scene.IsSaved = true;
                    _closingScenes.Remove(key);
                    _discardedScenes.Add(scene);
                    i--;
                }
            }
        }

        if (nothingSelected) ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button("Cancel"u8, buttonSize))
        {
            foreach (Scene scene in _discardedScenes)
                scene.IsSaved = false;

            Close();
            ImGui.EndPopup();
            return;
        }

        // Once all stages have been saved, signal that the program can exit.
        if (_closingScenes.Count == 0)
        {
            Close();
            window.WindowManager.Stop();
        }

        ImGui.EndPopup();
    }

    private void Close()
    {
        _discardedScenes.Clear();

        _closingScenes = null;
        _isOpened = false;

        ImGui.CloseCurrentPopup();
    }
}
