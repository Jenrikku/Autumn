using Autumn.Rendering;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Editors;

/// <summary>
/// A window that makes it possible to open stages from the project.
/// </summary>
internal class StageWindow
{
    private const ImGuiTableFlags _stageTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    int currentItem = 0;
    string[] comboStrings;
    MainWindowContext window;

    public StageWindow(MainWindowContext _window)
    {
        window = _window;
        comboStrings =
        [
            "All stages",
            "World 1",
            "World 2",
            "World 3",
            "World 4",
            "World 5",
            "World 6",
            "World 7",
            "World 8 (Part 1)",
            "World 8 (Part 2)"
        ];
        comboStrings = comboStrings
            .Concat(
                [
                    "Special 1",
                    "Special 2",
                    "Special 3",
                    "Special 4",
                    "Special 5",
                    "Special 6",
                    "Special 7",
                    "Special 8"
                ]
            )
            .ToArray();
    }

    public void Render()
    {
        if (!ImGui.Begin("Stages"))
            return;

        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - 16);
        ImGui.Combo("##typeselect", ref currentItem, comboStrings, comboStrings.Length);

        // Stage table:

        if (ImGui.BeginTable("stageTable", 2, _stageTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            //ImGui.TableSetupColumn("Position"); // (Relative to world map)
            ImGui.TableSetupColumn("Stage");
            ImGui.TableSetupColumn("Scenario", ImGuiTableColumnFlags.None, 0.35f);
            ImGui.TableHeadersRow();
            if (currentItem == 0) //All stages
            {
                foreach (var (name, scenario) in window.ContextHandler.ProjectStages)
                {
                    if (scenario == 0)
                        continue;
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);

                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        Scene? scene = window.Scenes.Find(scene =>
                            scene.Stage.Name == name && scene.Stage.Scenario == scenario
                        );

                        if (scene is not null) // Stage already opened
                            window.CurrentScene = scene;
                        else
                        {
                            window.BackgroundManager.Add(
                                $"Loading stage \"{name + scenario}\"...",
                                manager =>
                                {
                                    Stage stage = window.ContextHandler.FSHandler.ReadStage(
                                        name,
                                        scenario
                                    );

                                    Scene newScene =
                                        new(
                                            stage,
                                            window.ContextHandler.FSHandler,
                                            window.GLTaskScheduler,
                                            ref manager.StatusMessageSecondary
                                        )
                                        {
                                            IsSaved = true
                                        };

                                    newScene.ResetCamera();
                                    window.Scenes.Add(newScene);
                                }
                            );
                        }

                        ImGui.SetWindowFocus("Objects");
                    }

                    ImGui.TableNextColumn();

                    ImGui.Text(scenario.ToString());
                }
            }
            else
            {
                foreach (
                    SystemDataTable.StageDefine _stage in window
                        .ContextHandler.FSHandler.ReadGameSystemDataTable()
                        .WorldList[currentItem - 1]
                        .StageList
                )
                {
                    if (
                        !window.ContextHandler.ProjectStages.Contains(
                            (_stage.Stage, (byte)_stage.Scenario)
                        )
                    )
                        continue;
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);

                    if (ImGui.Selectable(_stage.Stage + _stage.Scenario, false))
                    {
                        Scene? scene = window.Scenes.Find(scene =>
                            scene.Stage.Name == _stage.Stage
                            && scene.Stage.Scenario == _stage.Scenario
                        );

                        if (scene is not null) // Stage already opened
                            window.CurrentScene = scene;
                        else
                        {
                            window.BackgroundManager.Add(
                                $"Loading stage \"{_stage.Stage + _stage.Scenario}\"...",
                                manager =>
                                {
                                    Stage stage = window.ContextHandler.FSHandler.ReadStage(
                                        _stage.Stage,
                                        (byte)_stage.Scenario
                                    );

                                    Scene newScene =
                                        new(
                                            stage,
                                            window.ContextHandler.FSHandler,
                                            window.GLTaskScheduler,
                                            ref manager.StatusMessageSecondary
                                        )
                                        {
                                            IsSaved = true
                                        };

                                    newScene.ResetCamera();
                                    window.Scenes.Add(newScene);
                                }
                            );
                        }

                        ImGui.SetWindowFocus("Objects");
                    }

                    ImGui.TableNextColumn();

                    ImGui.Text(_stage.Scenario.ToString());
                }
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }
}
