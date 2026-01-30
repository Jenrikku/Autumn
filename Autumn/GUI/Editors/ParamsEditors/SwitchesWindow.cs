using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class SwitchesWindow(MainWindowContext window)
{

    public bool IsOpen = false;
    public int SelectedSwitch = -1;
    private const ImGuiTableFlags _stageTableFlags = ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersOuter
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Resizable;
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; // | ImGuiDockNodeFlags.NoUndocking };
    public void Render()
    {
        if (!IsOpen)
        {
            return;
        }
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }

        if (!ImGui.Begin("Switches##SwitchWindow", ref IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
            return;
        if (window.CurrentScene == null)
        {
            ImGui.TextDisabled("Please load a stage first");
            ImGui.End();
            return;
        }
        
        var style = ImGui.GetStyle();
        float prevW = ImGui.GetWindowWidth();
        Scene scn = window.CurrentScene;

        ImGuiWidgets.TextHeader("Stage Switches:");
        var sw = scn.GetSwitches();

        if (ImGui.BeginTable("SwitchSelect", 2, _stageTableFlags,
        new(default, ImGui.GetWindowHeight() / 2.6f / window.ScalingFactor)))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Switch", ImGuiTableColumnFlags.WidthStretch, 0.4f);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableHeadersRow();
            foreach (int _i in sw.Keys)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("switchselect" + _i);
                if (ImGui.Selectable(_i.ToString(), _i == SelectedSwitch, ImGuiSelectableFlags.SpanAllColumns))
                {
                    SelectedSwitch = _i;
                }

                ImGui.PopID();
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(sw[_i].ToString());

            }
            ImGui.EndTable();
        }

        if (SelectedSwitch > -1)
        {
            ImGuiWidgets.TextHeader($"Switch {SelectedSwitch}");
            var swObj = scn.GetObjectsFromSwitch(SelectedSwitch);
            if (swObj != null)
            {
                if (ImGui.BeginTable("ObjectSwitch", 2, _stageTableFlags))
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                    ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 2f);
                    ImGui.TableSetupColumn("Switch type", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();
                    int ii = 0;
                    foreach (IStageSceneObj sobj in swObj)
                    {
                        if (swObj.Count > ii && ii > 0 && swObj[ii - 1] == sobj)
                        {
                            ii += 1;
                            continue;
                        }
                        foreach (string s in sobj.StageObj.GetSwitches(SelectedSwitch))
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.PushID("objectselect" + ii + s);
                            if (ImGui.Selectable(sobj.StageObj.Name, false, ImGuiSelectableFlags.SpanAllColumns))
                            {
                                ChangeHandler.ToggleObjectSelection(window, scn.History, sobj.PickingId, true);

                                AxisAlignedBoundingBox aabb = sobj.AABB * sobj.StageObj.Scale;
                                scn!.Camera.LookFrom(sobj.StageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                            }
                            ImGui.PopID();
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(s);
                        }
                        ii += 1;

                    }
                    ImGui.EndTable();
                }
            }
        }

        ImGui.End();
    }
}