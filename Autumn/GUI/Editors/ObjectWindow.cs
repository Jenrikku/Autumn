using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

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

    private bool IsChildArea(StageObj stageObj)
    {
        return _objectFilterCurrent == (byte)StageObjType.Area + 1 && stageObj.Type == StageObjType.AreaChild;
    }

    private bool IsChild(StageObj stageObj)
    {
        return _objectFilterCurrent == (byte)StageObjType.Regular + 1 && stageObj.Type == StageObjType.Child;
    }

    private int selectedIndex = -1;
    private int nextIdx = -1;
    private int prevIdx = -1;
    bool manualClick = false;
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; //ImGuiWidgets.NO_TAB_BAR };

    public void Render()
    {
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
                ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
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
            [
                "All Objects",
                "Areas",
                "Camera Areas",
                "Regular Objects",
                "Goals",
                "Start Events",
                "Start Objects",
                "Demo Scene Objects",
                "Rail"
            ],
            9
        );

        List<uint> ints = new();
        bool doubleclick = false;
        if (ImGui.BeginTable("objectTable", 3, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Visible", ImGuiTableColumnFlags.WidthFixed, 0.15f);
            ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 0.50f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.10f);
            ImGui.TableHeadersRow();
            int listId = 0;

            foreach (ISceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
            {
                StageObj stageObj = obj.StageObj;
                if (_objectFilterCurrent != 0 && _objectFilterCurrent != (byte)stageObj.Type + 1
                    && !IsChild(stageObj) && !IsChildArea(stageObj))
                    continue;
                ints.Add(obj.PickingId);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                //ImGui.PushFont(window.FontPointers[1]);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));

                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new Vector4(1, 1, 1, 0)));
                Vector2[] clickRect =
                [
                    ImGui.GetCursorPos() + new Vector2(0, 84 - ImGui.GetScrollY()),
                    ImGui.GetCursorPos() + new Vector2(ImGui.GetColumnWidth() + 10, 114 - ImGui.GetScrollY())
                ];
                //ImGui.GetWindowDrawList().AddRectFilled(clickRect[0],clickRect[1], 0xff0000ff);
                //ImGui.GetWindowDrawList().AddCircle(ImGui.GetMousePos(), 20, 0xff00ff00);
                if (ImGui.IsMouseHoveringRect(clickRect[0], clickRect[1]))
                    ImGui.PushStyleColor(ImGuiCol.Text, 0x7fffffff & ImGui.GetColorU32(ImGuiCol.Text));
                else
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xffffffff & ImGui.GetColorU32(ImGuiCol.Text));

                if (ImGui.Button(obj.IsVisible ? IconUtils.EYE_OPEN : IconUtils.EYE_CLOSED, new(ImGui.GetColumnWidth(), default))) // Hide / Show
                {
                    ChangeHandler.ChangePropertyValue(
                        window.CurrentScene.History,
                        obj,
                        "IsVisible",
                        obj.IsVisible,
                        !obj.IsVisible
                    );
                }

                ImGui.PopStyleColor(4);
                //ImGui.PopFont();

                ImGui.TableSetColumnIndex(1);

                ImGui.PushID("SceneObjSelectable" + obj.PickingId);

                if (selectedIndex == (int)obj.PickingId && !obj.Selected && lastKeyPressed && !manualClick)
                {
                    ChangeHandler.ToggleObjectSelection(window, window.CurrentScene.History, obj.PickingId, true);
                    ImGui.SetScrollHereY();
                    nextIdx = (int)obj.PickingId;
                    prevIdx = (int)obj.PickingId;
                }

                if (selectedIndex != (int)obj.PickingId && obj.Selected && manualClick && selectedIndex > (int)obj.PickingId)
                {
                    manualClick = false;
                    selectedIndex = (int)obj.PickingId;
                }

                if (ImGui.Selectable(stageObj.Name, obj.Selected, ImGuiSelectableFlags.AllowDoubleClick, new(1000, 25)))
                {
                    // NEXT SELECTION = listId;
                    nextIdx = (int)obj.PickingId;
                    manualClick = true;
                    selectedIndex = (int)obj.PickingId;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) doubleclick = _objectFilterCurrent == 8 ? false : true; // REMOVE WHEN RAILS ARE FUNCTIONAL AND SELECTABLE                    
                }

                ImGui.SetItemTooltip(stageObj.Name);

                if (
                    window.CurrentScene.SelectedObjects.Count() <= 1
                    && !lastKeyPressed
                    && selectedIndex != (int)obj.PickingId
                    && obj.Selected
                    && !manualClick
                )
                {
                    selectedIndex = (int)obj.PickingId;
                    ImGui.SetScrollHereY();
                    nextIdx = (int)obj.PickingId;
                    prevIdx = (int)obj.PickingId;
                }

                listId++;
                ImGui.TableSetColumnIndex(2);

                ImGui.Text(stageObj.Type.ToString());
            }

            ImGui.EndTable();
        }
        if (nextIdx != -1)
        {
            if (ImGui.IsKeyDown(ImGuiKey.ModShift) && nextIdx != prevIdx)
            {
                int max;
                int init;
                if (prevIdx > nextIdx)
                {
                    max = ints.IndexOf((uint)prevIdx);
                    init = ints.IndexOf((uint)nextIdx);
                }
                else
                {
                    init = ints.IndexOf((uint)prevIdx);
                    max = ints.IndexOf((uint)nextIdx);
                }
                while (init <= max)
                {
                    if (!window.CurrentScene.IsObjectSelected(ints[init]))
                        ChangeHandler.ToggleObjectSelection(
                            window,
                            window.CurrentScene.History,
                            ints[init],
                            false
                        );
                    init++;
                }
            }
            else if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) && nextIdx != prevIdx)
            {

                ChangeHandler.ToggleObjectSelection(
                    window,
                    window.CurrentScene.History,
                    (uint)nextIdx,
                    false
                );
            }
            else
            {
                ChangeHandler.ToggleObjectSelection(
                    window,
                    window.CurrentScene.History,
                    (uint)nextIdx,
                    true
                );
            }
            if (doubleclick)
            {
                AxisAlignedBoundingBox aabb =
                    window.CurrentScene.SelectedObjects.First().AABB
                    * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                window.CurrentScene!.Camera.LookFrom(
                    window.CurrentScene.SelectedObjects.First().StageObj.Translation * 0.01f,
                    aabb.GetDiagonal() * 0.01f
                );
            }
            prevIdx = nextIdx;
            nextIdx = -1;
        }

        lastKeyPressed = false;
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
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
