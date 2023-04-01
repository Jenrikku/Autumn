using AutumnSceneGL.Storage;
using ImGuiNET;

namespace AutumnSceneGL.GUI.ImGUIWindows {
    internal class StageWindow {
        private const ImGuiTableFlags _stageTableFlags =
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter |
            ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable;

        public static void Render(StageEditorContext context) {
            if(!ImGui.Begin("Stages"))
                return;

            // Stage table:
            if(ImGui.BeginTable("stageTable", 2, _stageTableFlags)) {
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                //ImGui.TableSetupColumn("Position");
                ImGui.TableSetupColumn("Stage");
                ImGui.TableSetupColumn("Scenario", ImGuiTableColumnFlags.None, 0.35f);
                ImGui.TableHeadersRow();

                foreach(Stage stage in Project.Stages) {
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);

                    if(ImGui.Selectable(stage.Name, false, ImGuiSelectableFlags.AllowDoubleClick)
                        && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {

                        context.Scenes.Add(new(stage));

                        ImGui.SetWindowFocus("Objects");
                    }

                    ImGui.TableNextColumn();

                    ImGui.Text(stage.Scenario.ToString() ?? string.Empty);
                }

                ImGui.EndTable();
            }

            ImGui.End();
        }
    }
}
