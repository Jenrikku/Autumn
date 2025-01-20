using System.Numerics;
using System.Text;
using Autumn.Enums;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;
using SceneGL.GLHelpers;
using SharpYaml.Schemas;
using Silk.NET.GLFW;
using SPICA;
namespace Autumn.GUI.Editors;

internal class ObjectWindow(MainWindowContext window)
{
    private int _objectFilterCurrent = 0;
    private bool lastKeyPressed = false;
    private const ImGuiTableFlags _objectTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    private bool isChildArea(StageObj stageObj)
    {
        return _objectFilterCurrent == (byte)StageObjType.Area + 1 && stageObj.Type == StageObjType.AreaChild;
    }
    private bool isChild(StageObj stageObj)
    {
        return _objectFilterCurrent == (byte)StageObjType.Regular + 1 && stageObj.Type == StageObjType.Child;
    }
    private int selectedIndex = -1;
    bool manualClick = false;
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
            ["All Objects", "Areas", "Camera Areas", "Regular Objects", "Goals", "Start Events", "Start Objects", "Demo Scene Objects", "Rail"],
            9
        );
        if (ImGui.BeginTable("objectTable", 3, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Visible", ImGuiTableColumnFlags.WidthFixed , 0.15f);
            ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch , 0.50f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch , 0.10f);
            ImGui.TableHeadersRow();
            int listId = 0;
            foreach (SceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
            {
                StageObj stageObj = obj.StageObj;
                if ((_objectFilterCurrent != 0 && _objectFilterCurrent != (byte)stageObj.Type + 1) && (!isChild(stageObj) && !isChildArea(stageObj)))
                    continue;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushFont(window.FontPointers[1]);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                Vector2[] clickRect = { ImGui.GetCursorPos() + new Vector2(0, 84 - ImGui.GetScrollY()), ImGui.GetCursorPos() + new Vector2(ImGui.GetColumnWidth()+10, 114- ImGui.GetScrollY()) };
                //ImGui.GetWindowDrawList().AddRectFilled(clickRect[0],clickRect[1], 0xff0000ff);
                //ImGui.GetWindowDrawList().AddCircle(ImGui.GetMousePos(), 20, 0xff00ff00);
                if (ImGui.IsMouseHoveringRect(clickRect[0], clickRect[1]))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, 0x7fffffff & ImGui.GetColorU32(ImGuiCol.Text));
                }
                else
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xffffffff& ImGui.GetColorU32(ImGuiCol.Text));
                if (ImGui.Button(obj.isVisible ?  "\uF06E" : "\uF070", new(ImGui.GetColumnWidth(), default))) // Hide / Show
                {
                    ChangeHandler.ChangeFieldValue(window.CurrentScene.History, obj, "isVisible", obj.isVisible, !obj.isVisible);
                }
                ImGui.PopStyleColor(4);
                ImGui.PopFont();

                ImGui.TableSetColumnIndex(1);

                ImGui.PushID("SceneObjSelectable" + obj.PickingId);
                if (window.CurrentScene.SelectedObjects.Count() <= 1 && !lastKeyPressed && selectedIndex != listId && obj.Selected && !manualClick)
                {
                    selectedIndex = listId;
                    ImGui.SetScrollHereY();
                }
                if (selectedIndex == listId && !obj.Selected && lastKeyPressed) 
                {
                    ChangeHandler.ToggleObjectSelection(
                        window,
                        window.CurrentScene.History,
                        obj,
                        true
                    );
                    ImGui.SetScrollHereY();
                }
                if (selectedIndex != listId && obj.Selected && manualClick)
                {
                    manualClick = false;
                    selectedIndex = listId;
                }
                if (ImGui.Selectable(stageObj.Name, obj.Selected, ImGuiSelectableFlags.AllowDoubleClick, new(1000,25))) 
                {
                    ChangeHandler.ToggleObjectSelection(
                        window,
                        window.CurrentScene.History,
                        obj,
                        !window.Keyboard?.IsShiftPressed() ?? true
                    );
                    manualClick = true;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) 
                    {
                        AxisAlignedBoundingBox aabb = window.CurrentScene.SelectedObjects.First().Actor.AABB * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                        window.CurrentScene!.Camera.LookFrom(window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f, aabb.GetDiagonal()*0.01f);
                    }
                }
                listId++;
                ImGui.TableSetColumnIndex(2);

                ImGui.Text(stageObj.Type.ToString());
            }


            ImGui.EndTable();
        }
        lastKeyPressed = false;
        if (window.IsFocused)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                selectedIndex += 1;
                selectedIndex = Math.Clamp(selectedIndex, 0, window.CurrentScene.CountSceneObjs() - 1);
                lastKeyPressed = true;
                manualClick = false;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                selectedIndex -= 1;
                selectedIndex = Math.Clamp(selectedIndex, 0, window.CurrentScene.CountSceneObjs() - 1);
                lastKeyPressed = true;
                manualClick = false;
            }
        }
        ImGui.End();
    }
}
