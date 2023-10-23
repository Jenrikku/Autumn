using Autumn.Scene;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class ObjectWindow
{
    private static int _objectFilterCurrent = 0;

    private const ImGuiTableFlags _objectTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    public static void Render(MainWindowContext context)
    {
        if (!ImGui.Begin("Objects"))
            return;

        if (context.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            return;
        }

        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X * 2);

        ImGui.Combo(
            "",
            ref _objectFilterCurrent,
            "All Objects\0Regular Objects\0Areas\0Camera Areas\0Goals\0Event Starts\0Start Objects",
            7
        );

        if (ImGui.BeginTable("objectTable", 2, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Object");
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
            ImGui.TableHeadersRow();

            if (
                (!context.CurrentScene?.Stage.Loaded ?? false)
                || (!context.CurrentScene?.IsReady ?? false)
            )
            {
                ImGui.EndTable();
                ImGui.End();
                return;
            }

            foreach (SceneObj obj in context.CurrentScene!.SceneObjects)
            {
                StageObj stageObj = obj.StageObj;

                if (_objectFilterCurrent != 0 && _objectFilterCurrent != (byte)stageObj.Type - 1)
                    continue;

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);

                ImGui.Selectable(stageObj.Name);

                ImGui.TableNextColumn();

                ImGui.Text(stageObj.Type.ToString());
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }
}
