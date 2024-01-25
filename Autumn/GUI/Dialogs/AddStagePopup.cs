using System.Numerics;
using Autumn.IO;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// Renders a dialog that allows to the user to open a stage from the RomFS.
/// </summary>
internal class AddStagePopup
{
    private WindowContext _context;

    private string _name = string.Empty;
    private string _scenario = string.Empty;

    private int _useRomFSComboCurrent = 0;

    private bool _stageListNeedsRebuild = true;
    private List<(string Name, byte Scenario)>? _foundStages;

    private bool _isOpened = false;

    /// <summary>
    /// Whether to reset the dialog to its defaults values once the "Ok" button has been pressed.
    /// </summary>
    public bool ResetOnDone;

    public AddStagePopup(WindowContext context, bool resetOnDone = true)
    {
        _context = context;
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
        if (!RomFSHandler.RomFSAvailable)
            _isOpened = false;

        if (!_isOpened)
            return;

        ImGui.OpenPopup("Add New Stage");

        Vector2 dimensions = new(450 * _context.ScalingFactor, 0);
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

        float scenarioFieldWidth = 30 * _context.ScalingFactor;

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
            _foundStages = RomFSHandler.StageNames.FindAll(
                (t) => t.Name.Contains(_name, StringComparison.InvariantCultureIgnoreCase)
            );

            // Remove already opened stages:
            foreach (Stage stage in ProjectHandler.ActiveProject.Stages)
                _foundStages.RemoveAll((t) => t.Name == stage.Name && t.Scenario == stage.Scenario);

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

        float okButtonWidth = 50 * _context.ScalingFactor;

        if (!RomFSHandler.StageNames.Contains((_name, scenarioNo)))
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

        Predicate<Stage> predicate = (stage) => stage.Name == _name && stage.Scenario == scenarioNo;
        bool stageExists = ProjectHandler.ActiveProject.Stages.Find(predicate) is not null;

        if (string.IsNullOrEmpty(_name) || stageExists)
            ImGui.BeginDisabled();

        if (ImGui.Button("Ok", new(okButtonWidth, 0)))
        {
            if (_useRomFSComboCurrent == 0)
            {
                _context.BackgroundManager.Add(
                    $"Importing stage \"{_name + scenarioNo}\" from RomFS...",
                    () =>
                    {
                        if (StageHandler.TryImportStage(_name, scenarioNo, out Stage stage))
                            ProjectHandler.ActiveProject.Stages.Add(stage);

                        if (ResetOnDone)
                            Reset();
                    }
                );
            }
            else
            {
                _context.BackgroundManager.Add(
                    $"Creating the stage \"{_name + scenarioNo}\"...",
                    () =>
                    {
                        Stage stage = StageHandler.CreateNewStage(_name, scenarioNo);
                        ProjectHandler.ActiveProject.Stages.Add(stage);

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
