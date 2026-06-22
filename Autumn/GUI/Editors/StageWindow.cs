using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using Hexa.NET.ImGui;

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
            "World 8 (Part 2)",
            "Special 1",
            "Special 2",
            "Special 3",
            "Special 4",
            "Special 5",
            "Special 6",
            "Special 7",
            "Special 8"
        ];
    }
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; //ImGuiWidgets.NO_TAB_BAR };

    public void Render()
    {
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
                ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (!ImGui.Begin("Stages"))
            return;

        if (ImGui.Button(IconUtils.PLUS))
        {
            window.ContextHandler.ActionHandler.ExecuteAction(CommandID.AddStage, window);
        }
        ImGui.SetItemTooltip("Add stage");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (window.ContextHandler.FSHandler.ReadGameSystemDataTable() == null)
            ImGui.BeginDisabled();
        ImGui.Combo("##typeselect", ref currentItem, comboStrings, comboStrings.Length);

        if (window.ContextHandler.FSHandler.ReadGameSystemDataTable() == null)
        {
            ImGui.EndDisabled();
            ImGui.SetItemTooltip("Can't find GameSystemDataTable.szs, so world selection is disabled");
        }
        
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
                    if (scenario == 0) // Why?
                        continue;
                    
                    StageSelectable(name, scenario);
                }
            }
            else
            {
                foreach (
                    SystemDataTable.StageDefine _stage in window
                        .ContextHandler.FSHandler.ReadGameSystemDataTable()!
                        .WorldList[currentItem - 1].StageList
                )
                {
                    if (!window.ContextHandler.ProjectStages.Contains((_stage.Stage, (byte)_stage.Scenario)))
                        continue;

                    StageSelectable(_stage.Stage, (byte)_stage.Scenario);
                }
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }

    private void StageSelectable(string stageName, byte scenario)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);

        if (ImGui.Selectable(stageName +$"##{stageName}{scenario}", false, ImGuiSelectableFlags.SpanAllColumns))
        {
            Scene? scene = window.Scenes.Find(scene =>
                scene.Stage.Name == stageName && scene.Stage.Scenario == scenario
            );

            if (scene is not null) // Stage already opened
            {
                window.CurrentScene = scene;
                window.SetSceneChange();
            }
            else
            {
                window.BackgroundManager.Add(
                    $"Loading stage \"{stageName + scenario}\"...",
                    manager =>
                    {
                        Stage stage = window.ContextHandler.FSHandler.ReadStage(stageName, scenario);

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
                        window.CurrentScene = newScene;
                        window.SetSceneChange();
                        ImGui.SetWindowFocus("Objects");
                    }
                );
            }

        }

        ImGui.TableNextColumn();

        ImGui.Text(scenario.ToString());
    }
}
