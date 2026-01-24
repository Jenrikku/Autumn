using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class LightParamsWindow(MainWindowContext window)
{

    public bool IsOpen = false;
    StageLight? _selectedlight;
    int _lightIdx;
    int _selectedlightarea = -1;
    int selectedlightName = -1;
    ImGuiWidgets.InputComboBox lightAreaCombo = new();

    string[] lightTypes = ["Map Obj Light", "Obj Light", "Player Light", "Stage Map Light"];
    int copyLight = 0;

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
            if (window.CurrentScene != null && window.CurrentScene.PreviewLight != null) window.CurrentScene.PreviewLight = null;
            return;
        }
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        
        if (!ImGui.Begin("Lights##LightParams", ref IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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

        if (ImGui.BeginTabBar("Ltabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Light Params"))
            {
                ImGuiWidgets.TextHeader("Light Params:");
                if (scn.Stage.LightParams is null && ImGui.Button("Enable Light Params for this stage", new(-1, default)))
                {
                    scn.Stage.LightParams = new();
                }
                else if (scn.Stage.LightParams is not null)
                {
                    if (ImGui.Button("Disable Light Params for this stage", new(-1, default)))
                    {
                        scn.Stage.LightParams = null;
                        scn.PreviewLight = null;
                    }
                    else
                        LightParamTab(scn, prevW, style);
                }
                ImGui.EndTabItem();
            }
            else
                scn.PreviewLight = null;
            if (ImGui.BeginTabItem("Light Areas"))
            {
                LightAreaTab(scn, prevW, style);
                ImGui.EndTabItem();
            }
        }
        ImGui.EndTabBar();

        ImGui.End();
    }


    void LightParamTab(Scene scn, float prevW, ImGuiStylePtr style)
    {
        ImGui.PushItemWidth(prevW - PROP_WIDTH - 20);
        ImGuiWidgets.DragInt("InterpolateFrame", ref scn.Stage.LightParams.InterpolateFrame, 1);
        ImGui.BeginDisabled();
        ImGuiWidgets.InputText("Name", ref scn.Stage.LightParams.Name, 128);
        ImGui.EndDisabled();
        ImGui.PopItemWidth();
        ImGui.PushItemWidth(prevW - style.ItemSpacing.X * 2);

        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.GetColorU32(ImGuiCol.FrameBg) | 0xFF000000);
        bool iscliked = false;
        if (ImGui.Selectable("Map Object Light", _lightIdx == 0))
        {
            _lightIdx = 0;
            iscliked = true;
        }
        if (ImGui.Selectable("Object Light", _lightIdx == 1))
        {
            _lightIdx = 1;
            iscliked = true;
        }
        if (ImGui.Selectable("Player Light", _lightIdx == 2))
        {
            _lightIdx = 2;
            iscliked = true;
        }
        if (ImGui.Selectable("Stage Map Light", _lightIdx == 3))
        {
            _lightIdx = 3;
            iscliked = true;
        }

        if (iscliked && ImGui.IsKeyDown(ImGuiKey.ModShift))
            _lightIdx = -1;

        ImGui.PopStyleColor();
        ImGui.Separator();
        _selectedlight = _lightIdx switch
        {
            0 => scn.Stage.LightParams.MapObjectLight,
            1 => scn.Stage.LightParams.ObjectLight,
            2 => scn.Stage.LightParams.PlayerLight,
            3 => scn.Stage.LightParams.StageMapLight,
            _ => null,
        };
        ImGui.PushItemWidth(prevW - PROP_WIDTH);
        if (scn.PreviewLight != _selectedlight)
        {
            scn.PreviewLight = _selectedlight;
        }
        if (_selectedlight != null)
        {
            if (ImGui.Button("Copy to:"))
            {
                switch (copyLight)
                {
                    case 0:
                        scn.Stage.LightParams.MapObjectLight = new(_selectedlight);
                        break;
                    case 1:
                        scn.Stage.LightParams.ObjectLight = new(_selectedlight)!;
                        break;
                    case 2:
                        scn.Stage.LightParams.PlayerLight = new(_selectedlight)!;
                        break;
                    case 3:
                        scn.Stage.LightParams.StageMapLight = new(_selectedlight)!;
                        break;
                }

            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(prevW - ImGui.CalcTextSize("Copy to:").X - style.ItemSpacing.X * 4);
            ImGui.Combo("##CopyTo", ref copyLight, lightTypes, lightTypes.Length);
            ImGui.Separator();
            int A = 22;
            int B = 30;

            ImGuiWidgets.PrePropertyWidthName("Follow Camera", A, B);
            ImGui.Checkbox("##Follow Camera", ref _selectedlight.IsCameraFollow);

            ImGuiWidgets.PrePropertyWidthName("Direction", A, B);
            ImGui.DragFloat3("##Direction", ref _selectedlight.Direction, 0.01f);

            ImGuiWidgets.PrePropertyWidthName("Ambient", A, B);
            ImGui.ColorEdit4("##Ambient", ref _selectedlight.Ambient, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Diffuse", A, B);
            ImGui.ColorEdit4("##Diffuse", ref _selectedlight.Diffuse, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Specular 0", A, B);
            ImGui.ColorEdit4("##Specular 0", ref _selectedlight.Specular0, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Specular 1", A, B);
            ImGui.ColorEdit4("##Specular 1", ref _selectedlight.Specular1, _colorEditFlags);
            //ImGui.Separator();

            // for (int i = 0; i < 6; i++)
            // {
            //     if (selectedlight.ConstantColors[i] != null)
            //     {

            //         if (ImGui.Button($"X##{i}"))
            //         {
            //             selectedlight.ConstantColors[i] = null;
            //             continue;
            //         }
            //         ImGui.SameLine();
            //         Vector4 ColorEdit = (Vector4)selectedlight.ConstantColors[i]!;
            //         ImGui.ColorEdit4($"Constant {i}", ref ColorEdit, _colorEditFlags);
            //         if (ColorEdit != selectedlight.ConstantColors[i])
            //         {
            //             selectedlight.ConstantColors[i] = ColorEdit;
            //         }
            //     }
            //     else
            //     {
            //         if (ImGui.Button($"Add Constant {i}"))
            //         {
            //             selectedlight.ConstantColors[i] = new();
            //         }
            //     }
            // }
            if (_selectedlight.ConstantColors[5] != null)
            {


                Vector4 ColorEdit = (Vector4)_selectedlight.ConstantColors[5]!;

                ImGui.Text("Constant 5");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Constant 5", 22, 30) - 24 * window.ScalingFactor);
                ImGui.ColorEdit4("##Constant 5", ref ColorEdit, _colorEditFlags);
                if (ColorEdit != _selectedlight.ConstantColors[5])
                {
                    _selectedlight.ConstantColors[5] = ColorEdit;
                }
                ImGui.SameLine();
                if (ImGui.Button($"X##5"))
                {
                    _selectedlight.ConstantColors[5] = null;
                }
            }
            else
            {
                if (ImGui.Button("Add Constant 5"))
                {
                    _selectedlight.ConstantColors[5] = new();
                }
            }

        }
        ImGui.PopItemWidth();
    }
    void LightAreaTab(Scene scn, float prevW, ImGuiStylePtr style)
    {
        ImGuiWidgets.TextHeader("Light Areas:");
        if (ImGui.BeginTable("LAreaSelect", 2, _stageTableFlags,

        new(default, ImGui.GetWindowHeight() / 3.2f / window.ScalingFactor)))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Area Id", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 1f);
            ImGui.TableHeadersRow();
            if (scn.Stage.LightAreaNames != null && scn.Stage.LightAreaNames.Count > 0)
            {   
                if (_selectedlightarea >= scn.Stage.LightAreaNames.Count) _selectedlightarea = 0;
                for (int _i = 0; _i < scn.Stage.LightAreaNames.Count; _i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushID("lightarea" + scn.Stage.LightAreaNames.Keys.ElementAt(_i));
                    var st = scn.Stage.LightAreaNames.Keys.ElementAt(_i).ToString();

                    if (ImGui.Selectable(st, _i == _selectedlightarea, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selectedlightarea = _i;
                    }

                    ImGui.PopID();
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(_i)]);

                }
            }
        }
        ImGui.EndTable();
        bool _disabled = _selectedlightarea < 0 || (scn.Stage.LightAreaNames.Count == 0);

        if (!_disabled)
        {
            ImGuiWidgets.TextHeader("Light Area " + scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea) + ":");
        }

        if (_disabled)
            ImGui.BeginDisabled();
        if (ImGui.Button(IconUtils.MINUS, new(ImGui.GetWindowWidth() / 2 - 10, default)))
        {
            scn.Stage.LightAreaNames.Remove(scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea));
        }
        ImGui.SameLine(default, style.ItemSpacing.X / 2);
        if (_disabled)
            ImGui.EndDisabled();
        if (ImGui.Button(IconUtils.PLUS, new(ImGui.GetWindowWidth() / 2 - 10, default)))
        {
            scn.AddLight();
        }
        if (_selectedlightarea > scn.Stage.LightAreaNames.Count - 1)
            _selectedlightarea = -1;
        if (_selectedlightarea > -1)
        {

            ImGui.PushItemWidth(prevW - PROP_WIDTH - 20);
            var ReadLightAreas = window.ContextHandler.FSHandler.ReadLightAreas();
            var refStr = scn.Stage.LightAreaNames.Values.ElementAt(_selectedlightarea);
            string prevRefStr = refStr;

            int aId = scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea);
            ImGui.InputInt("Area Id", ref aId);
            if (aId != scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea) && !scn.Stage.LightAreaNames.ContainsKey(aId))
            {
                string tmpS = scn.Stage.LightAreaNames.Values.ElementAt(_selectedlightarea);
                scn.Stage.LightAreaNames.Remove(scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea));
                scn.Stage.LightAreaNames.Add(aId, tmpS);
            }

            if (ReadLightAreas != null)
            {

                //selectedlightName = ReadLightAreas.Keys.ToList().IndexOf(refStr);

                var p = ImGui.GetCursorPosX();
                ImGui.Text("Light Name:");
                ImGui.SameLine();
                var keyArray = ReadLightAreas.Keys.ToArray();
                lightAreaCombo.Use("Light Name", ref refStr, ReadLightAreas.Keys.ToList(), ImGui.GetContentRegionAvail().X);
                ImGui.SetCursorPosX(p);
                //ImGui.Combo("Light Name", ref selectedlightName, keyArray, ReadLightAreas.Keys.Count - 1);

                if (refStr != prevRefStr)
                    scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea)] = refStr;

                if (keyArray.Contains(refStr))
                {

                    ImGui.PushItemWidth(prevW - PROP_WIDTH - 20);
                    LightArea area = ReadLightAreas![scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea)]];

                    ImGui.BeginDisabled();
                    ImGui.DragInt("InterpolateFrame", ref area.InterpolateFrame, 1);
                    ImGui.InputText("Name", ref area.Name, 128);
                    ImGui.PopItemWidth();
                    ImGui.PushItemWidth(prevW - style.ItemSpacing.X);
                    ImGui.EndDisabled();
                    ImGui.Separator();
                    ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.GetColorU32(ImGuiCol.FrameBg) | 0xFF000000);
                    if (ImGui.Selectable("Map Object Light", _lightIdx == 0))
                    {
                        _lightIdx = 0;
                    }
                    if (ImGui.Selectable("Object Light", _lightIdx == 1))
                    {
                        _lightIdx = 1;
                    }
                    if (ImGui.Selectable("Player Light", _lightIdx == 2))
                    {
                        _lightIdx = 2;
                    }
                    ImGui.PopStyleColor();
                    ImGui.Separator();

                    _selectedlight = _lightIdx switch
                    {
                        0 => area.MapObjectLight,
                        1 => area.ObjectLight,
                        2 => area.PlayerLight,
                        _ => null,
                    };
                    ImGui.PopItemWidth();
                    ImGui.PushItemWidth(prevW - PROP_WIDTH);
                    ImGui.BeginDisabled();
                    if (scn.PreviewLight != _selectedlight)
                    {
                        scn.PreviewLight = _selectedlight;
                    }
                    if (_selectedlight != null)
                    {
                        int A = 22;
                        int B = 30;

                        ImGuiWidgets.PrePropertyWidthName("Follow Camera", A, B);
                        ImGui.Checkbox("##Follow Camera", ref _selectedlight.IsCameraFollow);

                        ImGuiWidgets.PrePropertyWidthName("Direction", A, B);
                        ImGui.DragFloat3("##Direction", ref _selectedlight.Direction, 0.01f);

                        ImGuiWidgets.PrePropertyWidthName("Ambient", A, B);
                        ImGui.ColorEdit4("##Ambient", ref _selectedlight.Ambient, _colorEditFlags);

                        ImGuiWidgets.PrePropertyWidthName("Diffuse", A, B);
                        ImGui.ColorEdit4("##Diffuse", ref _selectedlight.Diffuse, _colorEditFlags);

                        ImGuiWidgets.PrePropertyWidthName("Specular 0", A, B);
                        ImGui.ColorEdit4("##Specular 0", ref _selectedlight.Specular0, _colorEditFlags);

                        ImGuiWidgets.PrePropertyWidthName("Specular 1", A, B);
                        ImGui.ColorEdit4("##Specular 1", ref _selectedlight.Specular1, _colorEditFlags);
                        for (int i = 0; i < 6; i++)
                        {
                            if (_selectedlight.ConstantColors[i] != null)
                            {
                                Vector4 ColorEdit = (Vector4)_selectedlight.ConstantColors[i]!;
                                ImGuiWidgets.PrePropertyWidthName($"Constant {i}", A, B);
                                ImGui.ColorEdit4($"##Constant {i}", ref ColorEdit, _colorEditFlags);
                                if (ColorEdit != _selectedlight.ConstantColors[i])
                                {
                                    _selectedlight.ConstantColors[i] = ColorEdit;
                                }
                            }
                        }

                    }
                    ImGui.PopItemWidth();
                    ImGui.EndDisabled();
                }
                else
                {
                    ImGui.BeginDisabled();
                    ImGui.TextWrapped("LightArea with that name couldn't be found in LightDataArea.szs!");
                    ImGui.Text("Can't display Light info.");
                    ImGui.EndDisabled();
                }
                ImGui.PopItemWidth();
            }
            else
            {
                var ln = scn.Stage.LightAreaNames.Values.ElementAt(_selectedlightarea);
                ImGui.InputText("Light Name", ref ln, 128);
                if (ln != scn.Stage.LightAreaNames.Values.ElementAt(_selectedlightarea))
                    scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(_selectedlightarea)] = ln;
                ImGui.TextDisabled("LightDataArea.szs not found.");
                ImGui.TextDisabled("Can't display Light info.");
            }
        }
    }


    
}