using System.Numerics;
using Autumn.FileSystems;
using Autumn.Rendering;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that allows the user to add a new empty stage or import it from the RomFS.
/// </summary>
internal class AddStageDialog
{
    private readonly MainWindowContext _window;

    private bool _isOpened = false;

    private string _name = string.Empty;
    private string _scenario = string.Empty;

    private int _useRomFSComboCurrent = 0;

    private bool _stageListNeedsRebuild = true;
    private List<(string Name, byte Scenario)>? _foundStages;

    /// <summary>
    /// Whether to reset the dialog to its defaults values once the "Ok" button has been pressed.
    /// </summary>
    public bool ResetOnDone;

    public AddStageDialog(MainWindowContext window, bool resetOnDone = true)
    {
        _window = window;
        ResetOnDone = resetOnDone;
        Reset();
    }

    public void Open() => _isOpened = true;

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        _name = string.Empty;
        _scenario = "1";
        _foundStages = null;
        _stageListNeedsRebuild = true;
    }

    public void Render()
    {
        if (_window.ContextHandler.Settings.RomFSPath is null)
            _isOpened = false;

        if (!_isOpened)
            return;

        ImGui.OpenPopup("Add New Stage");

        Vector2 dimensions = new(450 * _window.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Add New Stage",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        ImGuiStylePtr style = ImGui.GetStyle();

        if (!byte.TryParse(_scenario, out byte scenarioNo))
            scenarioNo = 0;

        # region Name and Scenario

        float scenarioFieldWidth = 30 * _window.ScalingFactor;

        ImGui.SetNextItemWidth(contentAvail.X - scenarioFieldWidth - style.ItemSpacing.X);
        if (ImGui.InputTextWithHint("##name", "Name", ref _name, 100))
            _stageListNeedsRebuild = true;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(scenarioFieldWidth);
        ImGui.InputText("##scenario", ref _scenario, 2, ImGuiInputTextFlags.CharsDecimal);

        ImGui.Spacing();

        # endregion

        # region Generate stage list

        if (_stageListNeedsRebuild)
        {
            RomFSHandler romFSHandler = new(_window.ContextHandler.Settings.RomFSPath!);

            _foundStages = romFSHandler
                .EnumerateStages()
                .Where(t => t.Name.ToLower().Contains(_name.ToLower()))
                .ToList();

            // Remove already opened stages:
            foreach (var stage in _window.ContextHandler.ProjectStages)
                _foundStages.Remove(stage);

            _foundStages.Sort();
            _stageListNeedsRebuild = false;
        }

        # endregion

        # region Stage list box

        ImGui.SetNextItemWidth(contentAvail.X);

        if (ImGui.BeginListBox("##stageList") && _foundStages is not null)
        {
            foreach (var (name, scenario) in _foundStages)
            {
                string visibleString = name + scenario;
                bool selected = _name == name && scenarioNo == scenario;

                if (ImGui.Selectable(visibleString, selected))
                {
                    _name = name;
                    _scenario = scenario.ToString();
                    _stageListNeedsRebuild = true;
                    _useRomFSComboCurrent = 0;
                }
            }

            ImGui.EndListBox();
        }

        # endregion

        # region Bottom bar

        float okButtonWidth = 50 * _window.ScalingFactor;

        if (!_foundStages?.Contains((_name, scenarioNo)) ?? true)
        {
            ImGui.BeginDisabled();
            _useRomFSComboCurrent = 1;
        }

        ImGui.SetNextItemWidth(contentAvail.X - okButtonWidth - style.ItemSpacing.X);

        ImGui.Combo(
            "##useRomFSCombo",
            ref _useRomFSComboCurrent,
            "Import the stage from the RomFS\0Create a new empty stage"
        );

        ImGui.EndDisabled();

        ImGui.SameLine();

        bool stageExists = _window.ContextHandler.ProjectStages.Contains((_name, scenarioNo));

        if (string.IsNullOrEmpty(_name) || stageExists)
            ImGui.BeginDisabled();

        if (ImGui.Button("Ok", new(okButtonWidth, 0)))
        {
            if (_useRomFSComboCurrent == 0)
            {
                _window.BackgroundManager.Add(
                    $"Importing stage \"{_name + scenarioNo}\" from RomFS...",
                    manager =>
                    {
                        Stage stage = _window.ContextHandler.FSHandler.ReadStage(_name, scenarioNo);
                        Scene scene =
                            new(
                                stage,
                                _window.ContextHandler.FSHandler,
                                _window.GLTaskScheduler,
                                ref manager.StatusMessageSecondary
                            );

                        _window.Scenes.Add(scene);

                        if (ResetOnDone)
                            Reset();
                    }
                );
            }
            else
            {
                _window.BackgroundManager.Add(
                    $"Creating the stage \"{_name + scenarioNo}\"...",
                    manager =>
                    {
                        Stage stage = new() { Name = _name, Scenario = scenarioNo };
                        Scene scene =
                            new(
                                stage,
                                _window.ContextHandler.FSHandler,
                                _window.GLTaskScheduler,
                                ref manager.StatusMessageSecondary
                            );

                        _window.Scenes.Add(scene);

                        if (ResetOnDone)
                            Reset();
                    }
                );
            }

            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndDisabled();

        // Warn the user if the stage already exists:
        if (stageExists)
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "A stage with this name already exists.");

        # endregion

        ImGui.EndPopup();
    }
}
