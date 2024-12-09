using System.Numerics;
using System.Text;
using Autumn.Enums;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
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
        if (ImGui.BeginTable("objectTable", 3, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Visible", ImGuiTableColumnFlags.WidthFixed , 0.15f);
            ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch , 0.50f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch , 0.10f);
            ImGui.TableHeadersRow();

            foreach (SceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
            {
                StageObj stageObj = obj.StageObj;

                if (_objectFilterCurrent != 0 && _objectFilterCurrent != (byte)stageObj.Type + 1)
                    continue;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushFont(window.FontPointers[1]);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.Header));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.Header));
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                if (ImGui.Button(obj.isVisible ?  "\uF06E" : "\uF070", new(40,30))) 
                {
                    obj.isVisible = !obj.isVisible;
                }
                ImGui.PopStyleColor(3);
                ImGui.PopFont();

                ImGui.TableSetColumnIndex(1);

                ImGui.PushID("SceneObjSelectable" + obj.PickingId);
                if (ImGui.Selectable(stageObj.Name, obj.Selected,ImGuiSelectableFlags.AllowDoubleClick, new(1000,25))) 
                {
                    ChangeHandler.ToggleObjectSelection(
                        window,
                        window.CurrentScene.History,
                        obj,
                        !window.Keyboard?.IsCtrlPressed() ?? true
                    );
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) 
                    {
                        AxisAlignedBoundingBox aabb = window.CurrentScene.SelectedObjects.First().Actor.AABB * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                        window.CurrentScene!.Camera.LookFrom(window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f, aabb.GetDiagonal()*0.01f);
                    }
                }

                ImGui.TableSetColumnIndex(2);

                ImGui.Text(stageObj.Type.ToString());
            }


            ImGui.EndTable();
        }

        ImGui.End();
    }
}
