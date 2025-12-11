using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class FogParamsWindow(MainWindowContext window)
{
    int selectedfog = -1;
    public bool _isOpen = false;
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

    public void Render()
    {
        if (!_isOpen)
        {
            return;
        }
        unsafe
        {
            ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; // | ImGuiDockNodeFlags.NoUndocking};
            ImGuiWindowClass* tmp = &windowClass;
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        
        if (!ImGui.Begin("Fog##FogParams", ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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

                if (ImGui.Selectable(st, _i == selectedfog, ImGuiSelectableFlags.SpanAllColumns))
                {
                    selectedfog = _i;
                }

                ImGui.PopID();
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(scn.GetFogCount(_i).ToString());

            }
        }
        ImGui.EndTable();

        //                ImGui.Text("Currently unsupported");

        if (scn.Stage.StageFogs.Count > selectedfog && selectedfog > -1)
        {

            string sfog = scn.Stage.StageFogs[selectedfog].AreaId == -1 ? "Main Fog" : "Fog " + scn.Stage.StageFogs[selectedfog].AreaId.ToString() + ":";
            ImGuiWidgets.TextHeader(sfog);
            // ImGui.SetWindowFontScale(1.20f);
            // if (scn.Stage.StageFogs[selectedfog].AreaId == -1)
            //     ImGui.Text("Main Fog:");
            // else
            //     ImGui.Text("Fog " + scn.Stage.StageFogs[selectedfog].AreaId.ToString() + ":");
            // ImGui.Separator();
            // ImGui.SetWindowFontScale(1f);
            bool _disabled = selectedfog == 0;
            if (_disabled)
                ImGui.BeginDisabled();
            if (ImGui.Button(IconUtils.MINUS, new(ImGui.GetWindowWidth() / 3 - 8, default)))
            {
                scn.RemoveFogAt(selectedfog);
                selectedfog -= 1;
            }
            if (_disabled)
                ImGui.EndDisabled();
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PASTE, new(ImGui.GetWindowWidth() / 3 - 8, default)))
            {
                scn.DuplicateFog(selectedfog);
                selectedfog = scn.CountFogs() - 1;
            }
            ImGui.SetItemTooltip("Duplicate Fog");
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PLUS, new(ImGui.GetWindowWidth() / 3 - 8, default)))
            {
                scn.AddFog(new() { AreaId = 0 });
                selectedfog = scn.CountFogs() - 1;
            }

            if (_disabled)
                ImGui.BeginDisabled();


            ImGui.PushItemWidth(prevW - PROP_WIDTH);

            var old = scn.Stage.StageFogs[selectedfog].AreaId;
            if (ImGuiWidgets.InputInt("Area Id", ref scn.Stage.StageFogs[selectedfog].AreaId))
            {
                scn.Stage.StageFogs[selectedfog].AreaId = int.Clamp(scn.Stage.StageFogs[selectedfog].AreaId, 0, 9999);
                scn.UpdateFog(selectedfog, old);
            }
            if (_disabled)
                ImGui.EndDisabled();

            ImGuiWidgets.DragFloat("Density", ref scn.Stage.StageFogs[selectedfog].Density);
            ImGuiWidgets.DragFloat("Min depth", ref scn.Stage.StageFogs[selectedfog].MinDepth);
            ImGuiWidgets.DragFloat("Max depth", ref scn.Stage.StageFogs[selectedfog].MaxDepth);
            ImGuiWidgets.InputInt("Interpolation", ref scn.Stage.StageFogs[selectedfog].InterpFrame);
            if (selectedfog == 0)
            {
                int fogtype = (int)scn.Stage.StageFogs[selectedfog].FogType;

                ImGui.Text("Type:");
                ImGui.SameLine();
                ImGuiWidgets.SetPropertyWidthGen("Type:");
                ImGui.Combo("##Type", ref fogtype, Enum.GetNames(typeof(StageFog.FogTypes)), 3);
                scn.Stage.StageFogs[selectedfog].FogType = (StageFog.FogTypes)fogtype;
            }
            ImGui.Text("Color:");
            ImGui.SameLine();
            ImGuiWidgets.SetPropertyWidthGen("Color:");
            ImGui.ColorEdit3("##Color", ref scn.Stage.StageFogs[selectedfog].Color,
            _colorEditFlags);
            ImGui.PopItemWidth();
            // ImGui.Text("Fogareas that use this fog");


            //TODO -> List of all fog areas using this fog
        }
        ImGui.End();
    }
}