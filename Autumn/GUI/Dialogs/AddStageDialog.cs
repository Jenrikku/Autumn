using System.Numerics;
using Autumn.FileSystems;
using Autumn.GUI.Windows;
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
    private byte scenarioNo = 0;
    private int _useRomFSComboCurrent = 0;

    private bool _stageListNeedsRebuild = true;
    private List<(string Name, byte Scenario)>? _foundStages;

    /// <summary>
    /// Whether to reset the dialog to its defaults values once the "Ok" button has been pressed.
    /// </summary>
    public bool ResetOnDone;
    int currentItem = 0;
    string[] comboStrings;
    bool _skipOk = false;

    public AddStageDialog(MainWindowContext window, bool resetOnDone = true)
    {
        _window = window;
        ResetOnDone = resetOnDone;

        comboStrings = ["All stages", "World 1", "World 2", "World 3", "World 4", "World 5", "World 6", "World 7", "World 8 (Part 1)", "World 8 (Part 2)",
                        "Special 1", "Special 2", "Special 3", "Special 4", "Special 5", "Special 6", "Special 7", "Special 8",
                        "Cutscenes", "Miscellaneous"];

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
        _skipOk = false;
    }

    public void Render()
    {
        if (_window.ContextHandler.FSHandler.OriginalFS == null)
            return;

        if (!_isOpened)
            return;
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Reset();
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("Add New Stage");

        Vector2 dimensions = new(450 * _window.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new(0.5f, 0.5f));

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

        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - 16);
        if (ImGui.Combo("##typeselect", ref currentItem, comboStrings, comboStrings.Length))
            _stageListNeedsRebuild = true;
        _skipOk = false;

        #region Name and Scenario

        float scenarioFieldWidth = 30 * _window.ScalingFactor;

        ImGui.SetNextItemWidth(contentAvail.X - scenarioFieldWidth - style.ItemSpacing.X);
        if (ImGui.InputTextWithHint("##name", "Name", ref _name, 100))
            _stageListNeedsRebuild = true;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(scenarioFieldWidth);
        ImGui.InputText("##scenario", ref _scenario, 2, ImGuiInputTextFlags.CharsDecimal);

        ImGui.Spacing();

        #endregion

        #region Generate stage list

        if (_stageListNeedsRebuild)
        {
            if (currentItem == 0)
            {
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains(_name, StringComparison.InvariantCultureIgnoreCase)).ToList();

                // Remove already opened stages:
                foreach (var stage in _window.ContextHandler.ProjectStages)
                    _foundStages.Remove(stage);

                _foundStages.Sort();
                _stageListNeedsRebuild = false;
            }
            else if (currentItem == comboStrings.Length - 2)
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains("Demo")).ToList();
            else if (currentItem == comboStrings.Length - 1)
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains("Mystery") || t.Name.Contains("Kinopio") || t.Name.Contains("Title") || t.Name.Contains("CourseSelect")).ToList();

        }

        #endregion

        #region Stage list box

        if (currentItem == 0)
        {

            ImGui.SetNextItemWidth(contentAvail.X);

            if (ImGui.BeginListBox("##stageList") && _foundStages is not null)
            {
                foreach (var (name, scenario) in _foundStages)
                {
                    string visibleString = name + scenario;
                    bool selected = _name == name && scenarioNo == scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = name;
                        _scenario = scenario.ToString();
                        _stageListNeedsRebuild = true;
                        _useRomFSComboCurrent = 0;

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            _skipOk = true;
                    }
                }

                ImGui.EndListBox();
            }
        }
        else if (currentItem > 0 && currentItem < comboStrings.Length - 2)
        {
            ImGui.SetNextItemWidth(contentAvail.X);

            if (
                ImGui.BeginListBox("##stageList")
                && _window.ContextHandler.FSHandler.ReadGameSystemDataTable() is not null
            )
            {
                foreach (
                    SystemDataTable.StageDefine _stage in _window
                        .ContextHandler.FSHandler.ReadGameSystemDataTable()!
                        .WorldList[currentItem - 1]
                        .StageList
                )
                {
                    if (
                        _stage.StageType == SystemDataTable.StageTypes.Empty
                        || _stage.StageType == SystemDataTable.StageTypes.Dokan
                    )
                        continue;

                    string visibleString = _stage.Stage + _stage.Scenario;
                    bool selected = _name == _stage.Stage && scenarioNo == _stage.Scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = _stage.Stage;
                        _scenario = _stage.Scenario.ToString();
                        _useRomFSComboCurrent = 0;

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            _skipOk = true;
                    }
                }

                ImGui.EndListBox();
            }
        }
        else
        {
            ImGui.SetNextItemWidth(contentAvail.X);

            if (ImGui.BeginListBox("##stageList") && _foundStages is not null)
            {
                foreach (var (name, scenario) in _foundStages)
                {
                    string visibleString = name + scenario;
                    bool selected = _name == name && scenarioNo == scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = name;
                        _scenario = scenario.ToString();
                        _stageListNeedsRebuild = true;
                        _useRomFSComboCurrent = 0;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            _skipOk = true;
                    }
                }

                ImGui.EndListBox();
            }
        }


        #endregion

        #region Bottom bar

        if (!byte.TryParse(_scenario, out scenarioNo))
            scenarioNo = 0;

        float okButtonWidth = 50 * _window.ScalingFactor;

        bool _disable = false;

        if (currentItem == 0 && (!_foundStages?.Contains((_name, scenarioNo)) ?? true))
        {
            _disable = true;
            _useRomFSComboCurrent = 1;
        }

        if (_window.ContextHandler.ProjectStages.Contains((_name, scenarioNo)))
            _disable = true;

        if (_disable)
            ImGui.BeginDisabled();

        ImGui.SetNextItemWidth(contentAvail.X - okButtonWidth - style.ItemSpacing.X);

        ImGui.Combo(
            "##useRomFSCombo",
            ref _useRomFSComboCurrent,
            ["Import the stage from the RomFS", "Create a new empty stage"],
            2
        );

        if (_disable)
            ImGui.EndDisabled();

        ImGui.SameLine();

        bool stageExists = _window.ContextHandler.ProjectStages.Contains((_name, scenarioNo));

        _disable = false;

        if (string.IsNullOrEmpty(_name) || stageExists)
            _disable = true;

        if (_disable)
            ImGui.BeginDisabled();

        if (ImGui.Button("Ok", new(okButtonWidth, 0)) || (_skipOk && !_disable))
        {
            if (_useRomFSComboCurrent == 0 || _skipOk)
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
                        scene.ResetCamera();
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
                        scene.ResetCamera();
                    }
                );
            }

            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        if (_disable)
            ImGui.EndDisabled();

        // Warn the user if the stage already exists:
        if (stageExists)
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "A stage with this name already exists.");

        #endregion

        ImGui.EndPopup();
    }
}
