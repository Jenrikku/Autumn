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
            ImGui.End();
            return;
        }

        if (ImGui.Button(IconUtils.PLUS))
        {
            window.ContextHandler.ActionHandler.ExecuteAction(CommandID.AddObject, window);
        }
        ImGui.SetItemTooltip("Add object");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.Combo(
            "",
            ref _objectFilterCurrent,
            [
                "All Objects",
                "Regular Objects",
                "Areas",
                "Camera Areas",
                "Goals",
                "Start Events",
                "Start Objects",
                "Demo Scene Objects",
                "Rails"
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
                if (_objectFilterCurrent != 0) // Object filtering
                {
                    if (obj is IStageSceneObj stageSceneObj && _objectFilterCurrent != (byte)stageSceneObj.StageObj.Type + 1
                        && !IsChild(stageSceneObj.StageObj) && !IsChildArea(stageSceneObj.StageObj))
                        continue;

                    if (obj is RailSceneObj && _objectFilterCurrent != (byte)StageObjType.Rail + 1)
                        continue;
                }

                ints.Add(obj.PickingId);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);

                if (ImGuiWidgets.HoverButton(obj.IsVisible ? IconUtils.EYE_OPEN : IconUtils.EYE_CLOSED, new(ImGui.GetColumnWidth(), 30))) // Hide / Show
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

                string name = obj switch
                {
                    ISceneObj x when x is IStageSceneObj y => y.StageObj.Name,
                    ISceneObj x when x is RailSceneObj y => y.RailObj.Name,
                    _ => string.Empty
                };

                string type = obj switch
                {
                    ISceneObj x when x is IStageSceneObj y => y.StageObj.Type.ToString(),
                    ISceneObj x when x is RailSceneObj y => "Rail",
                    _ => string.Empty
                };

                if (ImGui.Selectable(name, obj.Selected, ImGuiSelectableFlags.AllowDoubleClick, new(ImGui.GetColumnWidth(), 25)))
                {
                    // NEXT SELECTION = listId;
                    nextIdx = (int)obj.PickingId;
                    manualClick = true;
                    selectedIndex = (int)obj.PickingId;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) doubleclick = true;
                }

                ImGui.SetItemTooltip(name);

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

                ImGui.Text(type);
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
                window.CameraToObject(window.CurrentScene.SelectedObjects.First());
            }

            prevIdx = nextIdx;
            nextIdx = -1;
        }

        lastKeyPressed = false;
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                if (window.CurrentScene.SelectedObjects.First() is IStageSceneObj)
                {
                    selectedIndex += 1;
                    selectedIndex = Math.Clamp(selectedIndex, 0, window.CurrentScene.CountSceneObjs() - 1);
                }
                else
                    selectedIndex = (int)window.CurrentScene.GetNextRailId();
                lastKeyPressed = true;
                manualClick = false;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                if (window.CurrentScene.SelectedObjects.First() is IStageSceneObj)
                {
                    selectedIndex -= 1;
                    selectedIndex = Math.Clamp(selectedIndex, 0, window.CurrentScene.CountSceneObjs() - 1);
                }
                else
                    selectedIndex = (int)window.CurrentScene.GetPreviousRailId();
                lastKeyPressed = true;
                manualClick = false;
            }
        }

        ImGui.End();
    }
}
