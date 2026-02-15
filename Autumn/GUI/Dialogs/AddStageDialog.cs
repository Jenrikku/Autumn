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
    public void Open() => _isOpened = true;
    private string _name = string.Empty;
    private int _scenarioNo = 1;
    private int _useRomFSComboCurrent = 0;

    private bool _stageListNeedsRebuild = true;
    private List<(string Name, byte Scenario)>? _foundStages;

    /// <summary>
    /// Whether to reset the dialog to its defaults values once the "Ok" button has been pressed.
    /// </summary>
    private int _currentItem = 0;
    private string[] _comboStrings;
    private bool _skipOk = false;

    public AddStageDialog(MainWindowContext window)
    {
        _window = window;
        _comboStrings = ["All stages", "World 1", "World 2", "World 3", "World 4", "World 5", "World 6", "World 7", "World 8 (Part 1)", "World 8 (Part 2)",
                        "Special 1", "Special 2", "Special 3", "Special 4", "Special 5", "Special 6", "Special 7", "Special 8",
                        "Cutscenes", "Miscellaneous"];

        Reset();
    }

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        _name = string.Empty;
        _scenarioNo = 1;
        _foundStages = null;
        _stageListNeedsRebuild = true;
        _skipOk = false;
    }

    bool _noReference = false;

    public void Render()
    {
        if (_window.ContextHandler.FSHandler.OriginalFS == null)
            _noReference = true;

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
        if (_noReference) RenderNoRomfs();
        else RenderWithRomfs();
    }

    void RenderWithRomfs()
    {
        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        ImGuiStylePtr style = ImGui.GetStyle();
        ImGui.SetNextItemWidth(contentAvail.X);
        if (ImGui.Combo("##typeselect", ref _currentItem, _comboStrings, _comboStrings.Length))
            _stageListNeedsRebuild = true;
        _skipOk = false;

        #region Name and Scenario

        float scenarioFieldWidth = contentAvail.X / 4;

        ImGui.SetNextItemWidth(3 *scenarioFieldWidth - style.ItemInnerSpacing.X);
        if (ImGui.InputTextWithHint("##name", "Name", ref _name, 100))
            _stageListNeedsRebuild = true;

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.SetNextItemWidth(scenarioFieldWidth);
        ImGui.InputInt("##scenario", ref _scenarioNo, 1);
        _scenarioNo = _scenarioNo > 0 ? _scenarioNo < 0x100 ? _scenarioNo : 255 : 0;

        ImGui.Spacing();

        #endregion

        #region Generate stage list

        if (_stageListNeedsRebuild)
        {
            if (_currentItem == 0)
            {
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains(_name, StringComparison.InvariantCultureIgnoreCase)).ToList();

                // Remove already opened stages:
                foreach (var stage in _window.ContextHandler.ProjectStages)
                    _foundStages.Remove(stage);

                _foundStages.Sort();
                _stageListNeedsRebuild = false;
            }
            else if (_currentItem == _comboStrings.Length - 2)
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains("Demo")).ToList();
            else if (_currentItem == _comboStrings.Length - 1)
                _foundStages = _window.ContextHandler.FSHandler.OriginalFS.EnumerateStages().Where(t => t.Name.Contains("Mystery") || t.Name.Contains("Kinopio") || t.Name.Contains("Title") || t.Name.Contains("CourseSelect")).ToList();

        }

        #endregion

        #region Stage list box

        if (_currentItem == 0)
        {

            ImGui.SetNextItemWidth(contentAvail.X);

            if (ImGui.BeginListBox("##stageList") && _foundStages is not null)
            {
                foreach (var (name, scenario) in _foundStages)
                {
                    string visibleString = name + scenario;
                    bool selected = _name == name && _scenarioNo == scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = name;
                        _scenarioNo = scenario;
                        _stageListNeedsRebuild = true;
                        _useRomFSComboCurrent = 0;

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            _skipOk = true;
                    }
                }

                ImGui.EndListBox();
            }
        }
        else if (_currentItem > 0 && _currentItem < _comboStrings.Length - 2)
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
                        .WorldList[_currentItem - 1]
                        .StageList
                )
                {
                    if (
                        _stage.StageType == SystemDataTable.StageTypes.Empty
                        || _stage.StageType == SystemDataTable.StageTypes.Dokan
                    )
                        continue;

                    string visibleString = _stage.Stage + _stage.Scenario;
                    bool selected = _name == _stage.Stage && _scenarioNo == _stage.Scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = _stage.Stage;
                        _scenarioNo = _stage.Scenario;
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
                    bool selected = _name == name && _scenarioNo == scenario;

                    if (ImGui.Selectable(visibleString, selected, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _name = name;
                        _scenarioNo = scenario;
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


        float okButtonWidth = 50 * _window.ScalingFactor;

        bool _disable = false;

        if (_currentItem == 0 && (!_foundStages?.Contains((_name, (byte)_scenarioNo)) ?? true))
        {
            _disable = true;
            _useRomFSComboCurrent = 1;
        }

        if (_window.ContextHandler.ProjectStages.Contains((_name, (byte)_scenarioNo)))
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

        bool stageExists = _window.ContextHandler.ProjectStages.Contains((_name, (byte)_scenarioNo));

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
                    $"Importing stage \"{_name + _scenarioNo}\" from RomFS...",
                    manager =>
                    {
                        Stage stage = _window.ContextHandler.FSHandler.ReadStage(_name, (byte)_scenarioNo);
                        Scene scene =
                            new(
                                stage,
                                _window.ContextHandler.FSHandler,
                                _window.GLTaskScheduler,
                                ref manager.StatusMessageSecondary
                            );

                        _window.Scenes.Add(scene);

                        Reset();
                        scene.ResetCamera();
                        ImGui.SetWindowFocus("Objects");
                    }
                );
            }
            else
            {
                NewEmptyStage();
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
    void RenderNoRomfs()
    {
        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        ImGuiStylePtr style = ImGui.GetStyle();


        float scenarioFieldWidth = 80 * _window.ScalingFactor;

        ImGui.SetNextItemWidth(contentAvail.X - scenarioFieldWidth - style.ItemSpacing.X);
        if (ImGui.InputTextWithHint("##name", "Name", ref _name, 100))
            _stageListNeedsRebuild = true;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(scenarioFieldWidth);
        ImGui.InputInt("##scenario", ref _scenarioNo, 1);
        _scenarioNo = _scenarioNo > 0 ? _scenarioNo < 0x100 ? _scenarioNo : 255 : 0;
        bool stageExists = _window.ContextHandler.ProjectStages.Contains((_name, (byte)_scenarioNo));


        ImGui.Text("Create a new empty stage"); ImGui.SameLine();

        bool _disable = false;
        if (string.IsNullOrEmpty(_name) || stageExists)
            _disable = true;

        if (_disable)
            ImGui.BeginDisabled();

        if (ImGui.Button("Ok", new(-1, 0)) || (_skipOk && !_disable))
        {
            NewEmptyStage();
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        if (_disable)
            ImGui.EndDisabled();
        // Warn the user if the stage already exists:
        if (stageExists)
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "A stage with this name already exists.");

        ImGui.EndPopup();
    }

    private void NewEmptyStage()
    {
        _window.BackgroundManager.Add(
            $"Creating the stage \"{_name + _scenarioNo}\"...",
            manager =>
            {
                Stage stage = new() { Name = _name, Scenario = (byte)_scenarioNo };
                Scene scene =
                    new(
                        stage,
                        _window.ContextHandler.FSHandler,
                        _window.GLTaskScheduler,
                        ref manager.StatusMessageSecondary
                    );

                _window.Scenes.Add(scene);

                Reset();
                scene.ResetCamera();
                ImGui.SetWindowFocus("Objects");
            }
        );
    }
}
