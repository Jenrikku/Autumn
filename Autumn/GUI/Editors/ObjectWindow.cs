using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class ObjectWindow(MainWindowContext window)
{
    private int _objectFilterCurrent = 0;

    private const ImGuiTableFlags _objectTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    public void Render()
    {
        if (!ImGui.Begin("Objects"))
            return;

        if (window.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            return;
        }

        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X * 2);

        ImGui.Combo(
            "",
            ref _objectFilterCurrent,
            "All Objects\0Regular Objects\0Areas\0Camera Areas\0Goals\0Event Starts\0Start Objects\0Demo Scene Objects\0Rail",
            9
        );

        if (ImGui.BeginTable("objectTable", 2, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Object");
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
            ImGui.TableHeadersRow();

            foreach (SceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
            {
                StageObj stageObj = obj.StageObj;

                if (_objectFilterCurrent != 0 && _objectFilterCurrent != (byte)stageObj.Type + 1)
                    continue;

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);

                ImGui.PushID("SceneObjSelectable" + obj.PickingId);
                if (ImGui.Selectable(stageObj.Name, obj.Selected))
                    ChangeHandler.ToggleObjectSelection(
                        window,
                        window.CurrentScene.History,
                        obj,
                        !window.Keyboard?.IsCtrlPressed() ?? true
                    );

                ImGui.TableNextColumn();

                ImGui.Text(stageObj.Type.ToString());
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }
}
