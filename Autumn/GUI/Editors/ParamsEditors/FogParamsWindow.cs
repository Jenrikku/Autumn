using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class FogParamsWindow(MainWindowContext window)
{
    public bool IsOpen = false;
    int _selectedfog = -1;
    private const ImGuiTableFlags _stageTableFlags = ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersOuter
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Resizable;

    private const ImGuiColorEditFlags _colorEditFlags = ImGuiColorEditFlags.DisplayRGB
                | ImGuiColorEditFlags.NoSidePreview
                | ImGuiColorEditFlags.PickerHueBar
                | ImGuiColorEditFlags.Float
                | ImGuiColorEditFlags.NoAlpha; // We ignore alpha because the game seems to do so too
    private const float PROP_WIDTH = 105f;
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
        
        if (!ImGui.Begin("Fog##FogParams", ref IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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

         ImGuiWidgets.TextHeader("Stage Fogs:");
        bool autoResize = scn.Stage.StageFogs.Count < 12;
        var fg = scn.GetFogs();
        if (ImGui.BeginTable("FogSelect", 2, _stageTableFlags,
        new(default, ImGui.GetWindowHeight() / 2.6f / window.ScalingFactor)))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Fog Id", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.None, 1f);
            ImGui.TableHeadersRow();
            for (int _i = 0; _i < scn.Stage.StageFogs.Count; _i++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("fogselect" + _i);
                var st = _i == 0 ? "Main" : scn.Stage.StageFogs[_i].AreaId.ToString();

                if (ImGui.Selectable(st, _i == _selectedfog, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _selectedfog = _i;
                }

                ImGui.PopID();
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(scn.GetFogCount(_i).ToString());

            }
        }
        ImGui.EndTable();

        //                ImGui.Text("Currently unsupported");

        if (scn.Stage.StageFogs.Count > _selectedfog && _selectedfog > -1)
        {

            string sfog = scn.Stage.StageFogs[_selectedfog].AreaId == -1 ? "Main Fog" : "Fog " + scn.Stage.StageFogs[_selectedfog].AreaId.ToString() + ":";
            ImGuiWidgets.TextHeader(sfog);
            // ImGui.SetWindowFontScale(1.20f);
            // if (scn.Stage.StageFogs[selectedfog].AreaId == -1)
            //     ImGui.Text("Main Fog:");
            // else
            //     ImGui.Text("Fog " + scn.Stage.StageFogs[selectedfog].AreaId.ToString() + ":");
            // ImGui.Separator();
            // ImGui.SetWindowFontScale(1f);
            bool _disabled = _selectedfog == 0;
            float w = ImGui.GetContentRegionAvail().X;
            if (_disabled)
                ImGui.BeginDisabled();
            if (ImGui.Button(IconUtils.MINUS, new(w / 3 - style.ItemSpacing.X / 3, default)))
            {
                scn.RemoveFogAt(_selectedfog);
                _selectedfog -= 1;
            }
            if (_disabled)
                ImGui.EndDisabled();
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PASTE, new(w / 3 - style.ItemSpacing.X / 3, default)))
            {
                scn.DuplicateFog(_selectedfog);
                _selectedfog = scn.CountFogs() - 1;
            }
            ImGui.SetItemTooltip("Duplicate Fog");
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PLUS, new(w / 3 - style.ItemSpacing.X / 3, default)))
            {
                scn.AddFog(new() { AreaId = 0 });
                _selectedfog = scn.CountFogs() - 1;
            }

            if (_disabled)
                ImGui.BeginDisabled();


            ImGui.PushItemWidth(prevW - PROP_WIDTH);

            var old = scn.Stage.StageFogs[_selectedfog].AreaId;
            if (ImGuiWidgets.InputInt("Area Id", ref scn.Stage.StageFogs[_selectedfog].AreaId))
            {
                scn.Stage.StageFogs[_selectedfog].AreaId = int.Clamp(scn.Stage.StageFogs[_selectedfog].AreaId, 0, 9999);
                scn.UpdateFog(_selectedfog, old);
            }
            if (_disabled)
                ImGui.EndDisabled();

            ImGuiWidgets.DragFloat("Density", ref scn.Stage.StageFogs[_selectedfog].Density);
            ImGuiWidgets.DragFloat("Min depth", ref scn.Stage.StageFogs[_selectedfog].MinDepth);
            ImGuiWidgets.DragFloat("Max depth", ref scn.Stage.StageFogs[_selectedfog].MaxDepth);
            ImGuiWidgets.InputInt("Interpolation", ref scn.Stage.StageFogs[_selectedfog].InterpFrame);
            if (_selectedfog == 0)
            {
                int fogtype = (int)scn.Stage.StageFogs[_selectedfog].FogType;

                ImGui.Text("Type:");
                ImGui.SameLine();
                ImGuiWidgets.SetPropertyWidthGen("Type:");
                ImGui.Combo("##Type", ref fogtype, Enum.GetNames(typeof(StageFog.FogTypes)), 3);
                scn.Stage.StageFogs[_selectedfog].FogType = (StageFog.FogTypes)fogtype;
            }
            ImGui.Text("Color:");
            ImGui.SameLine();
            ImGuiWidgets.SetPropertyWidthGen("Color:");
            ImGui.ColorEdit3("##Color", ref scn.Stage.StageFogs[_selectedfog].Color,
            _colorEditFlags);
            ImGui.PopItemWidth();
            // ImGui.Text("Fogareas that use this fog");


            //TODO -> List of all fog areas using this fog
        }
        ImGui.End();
    }
}