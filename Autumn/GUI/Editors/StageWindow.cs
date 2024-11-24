using Autumn.Rendering;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Editors;

/// <summary>
/// A window that makes it possible to open stages from the project.
/// </summary>
internal class StageWindow(MainWindowContext window)
{
    private const ImGuiTableFlags _stageTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    public void Render()
    {
        if (!ImGui.Begin("Stages"))
            return;

        // Stage table:
        if (ImGui.BeginTable("stageTable", 2, _stageTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            //ImGui.TableSetupColumn("Position"); // (Relative to world map)
            ImGui.TableSetupColumn("Stage");
            ImGui.TableSetupColumn("Scenario", ImGuiTableColumnFlags.None, 0.35f);
            ImGui.TableHeadersRow();

            foreach (var (name, scenario) in window.ContextHandler.ProjectStages)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);

                if (ImGui.Selectable(name, false))
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

                    ImGui.SetWindowFocus("Scene");
                }

                ImGui.TableNextColumn();

                ImGui.Text(scenario.ToString());
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }
}
