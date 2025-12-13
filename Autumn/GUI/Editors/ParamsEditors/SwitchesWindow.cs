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

    public bool _isOpen = false;
    public int _selectedSwitch = -1;
    private const ImGuiTableFlags _stageTableFlags = ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersOuter
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Resizable;

    private const ImGuiColorEditFlags _colorEditFlags = ImGuiColorEditFlags.DisplayRGB
                | ImGuiColorEditFlags.NoSidePreview
                | ImGuiColorEditFlags.PickerHueBar
                | ImGuiColorEditFlags.Float
                | ImGuiColorEditFlags.NoAlpha; // We ignore alpha because the game seems to do so too

    public int CurrentTab = -1;
    private const float PROP_WIDTH = 105f;
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; // | ImGuiDockNodeFlags.NoUndocking };
    public void Render()
    {
        if (!_isOpen)
        {
            return;
        }
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }

        if (!ImGui.Begin("Switches##SwitchWindow", ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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
                if (ImGui.Selectable(_i.ToString(), _i == _selectedSwitch, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _selectedSwitch = _i;
                }

                ImGui.PopID();
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(sw[_i].ToString());

            }
            ImGui.EndTable();
        }

        if (_selectedSwitch > -1)
        {
            ImGuiWidgets.TextHeader($"Switch {_selectedSwitch}");
            var swObj = scn.GetObjectsFromSwitch(_selectedSwitch);
            if (swObj != null)
            {
                if (ImGui.BeginTable("ObjectSwitch", 2, _stageTableFlags))
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                    ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 2f);
                    ImGui.TableSetupColumn("Switch type", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();
                    int ii = 0;
                    foreach (ISceneObj sobj in swObj)
                    {
                        if (swObj.Count > ii && ii > 0 && swObj[ii - 1] == sobj)
                        {
                            ii += 1;
                            continue;
                        }
                        foreach (string s in sobj.StageObj.GetSwitches(_selectedSwitch))
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