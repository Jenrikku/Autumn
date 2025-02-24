using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Editors;

/// <summary>
/// A window that will contain all extra stage properties (Timer, music, fog, cameras, light and switches)
/// </summary>
internal class ParametersWindow(MainWindowContext window)
{
    public bool SwitchEnabled = false;
    public int SelectedSwitch = -1;
    public bool CamerasEnabled = false;
    public bool MiscEnabled = false;
    int musicIdx = 0;
    public bool FogEnabled = false;
    int selectedfog = -1;
    public bool LightEnabled = false;
    StageLight? selectedlight;
    int lightIdx;
    int selectedlightarea = -1;
    int selectedlightName = -1;
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

    public int CurrentTab = -1;
    private const float PROP_WIDTH = 105f;


    private bool IsEnabled => SwitchEnabled || MiscEnabled || CamerasEnabled || FogEnabled || LightEnabled;
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiWidgets.NO_TAB_BAR };

    public void Render()
    {
        if (!IsEnabled)
            return;
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
                ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (!ImGui.Begin("Params", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar))
            return;

        if (window.CurrentScene == null)
        {
            ImGui.TextDisabled("Please load a stage first");
            ImGui.End();
            return;
        }
        var style = ImGui.GetStyle();
        float prevW = ImGui.GetWindowWidth();
        var scn = window.CurrentScene;
        ImGui.GetIO().ConfigDragClickToInputText = true;
        if (ImGui.BeginTabBar("PTabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("General", ref MiscEnabled))
            {
                MiscParamTab(scn, prevW, style);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Camera", ref CamerasEnabled))
            {
                ImGui.Text("Currently unsupported");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Switches", ref SwitchEnabled, CurrentTab == 0 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                ImGui.SetWindowFontScale(1.20f);
                ImGui.Text("Stage Switches:");
                ImGui.Separator();
                ImGui.SetWindowFontScale(1f);
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
                }
                ImGui.EndTable();

                if (SelectedSwitch > -1)
                {

                    ImGui.SetWindowFontScale(1.20f);
                    ImGui.Text($"Switch {SelectedSwitch}");
                    ImGui.Separator();
                    ImGui.SetWindowFontScale(1f);
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
                            foreach (ISceneObj sobj in swObj)
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
                        }
                        ImGui.EndTable();
                    }
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Fog", ref FogEnabled))
            {
                FogAreasTab(scn, prevW, style);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Light Params", ref LightEnabled))
            {

                ImGui.SetWindowFontScale(1.20f);
                ImGui.Text("Light Params:");
                ImGui.Separator();
                ImGui.SetWindowFontScale(1f);
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
            if (ImGui.BeginTabItem("Light Areas", ref LightEnabled))
            {
                LightAreaTab(scn, prevW, style);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        CurrentTab = -1;

        //ImGui.Combo("##typeselect", ref currentItem, new string[] { "All stages", "World 1" }, 2);

        // Stage table:

        ImGui.End();
    }

    void MiscParamTab(Scene scn, float prevW, ImGuiStylePtr style)
    {
        ImGui.SetWindowFontScale(1.20f);
        ImGui.Text("Stage Params:");
        ImGui.Separator();
        ImGui.SetWindowFontScale(1f);

        ImGuiWidgets.InputInt("Timer", ref scn.Stage.StageParams.Timer, 10);
        ImGuiWidgets.InputInt("Restart", ref scn.Stage.StageParams.RestartTimer, 10);
        ImGuiWidgets.InputInt("MaxPow", ref scn.Stage.StageParams.MaxPowerUps, 1);
        ImGui.NewLine();
        ImGui.Separator();
        ImGui.SetWindowFontScale(1.20f);
        ImGui.Text("Stage Music:");
        ImGui.Separator();
        ImGui.SetWindowFontScale(1f);
        BgmTable? b = window.ContextHandler.FSHandler.ReadBgmTable();
        if (b != null)
        {
            ImGui.Text("Default Music:");
            var bgmdefault = b.StageDefaultBgmList.FirstOrDefault(x => x.StageName == scn.Stage.Name && x.Scenario == scn.Stage.Scenario);
            if (bgmdefault != null)
            {
                if (scn.Stage.DefaultBgm == null)
                    scn.Stage.DefaultBgm = new(bgmdefault);

                musicIdx = Array.IndexOf(b.BgmArray, scn.Stage.DefaultBgm.BgmLabel);
                int oldIdx = musicIdx;

                ImGui.SetNextItemWidth(prevW - 16 * window.ScalingFactor);
                ImGui.Combo("##musSelect", ref musicIdx, b.BgmArray, b.BgmFiles.Count);
                if (musicIdx != oldIdx)
                {
                    scn.Stage.DefaultBgm.BgmLabel = b.BgmFiles[musicIdx];
                }

            }
            else if (scn.Stage.DefaultBgm is null)
            {
                ImGui.SetNextItemWidth(prevW - 16 * window.ScalingFactor);
                ImGui.TextDisabled("This Stage isn't present in the Default Bgm List");
            }
            ImGui.Text("Music Areas:");
            var bgmlist = b.StageBgmList.FirstOrDefault(x => x.StageName == scn.Stage.Name && x.Scenario == scn.Stage.Scenario);
            if (bgmlist != null && bgmlist.LineList.ContainsKey("LineStage"))
            {
                if (ImGui.BeginTable("MusicAreaIdTables", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                    ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 0.22f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.7f);
                    ImGui.TableSetupColumn("Song", ImGuiTableColumnFlags.WidthStretch, 0.9f);
                    ImGui.TableHeadersRow();

                    foreach (KeyValuePair<int, string> s in b.BgmTypes)
                    {
                        //if (s.Value == "Stage") continue;
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(s.Key.ToString());
                        ImGui.TableSetColumnIndex(1);
                        BgmTable.KindDefine? sbgm = bgmlist.LineList["LineStage"].Where(x => x.Kind == s.Value).FirstOrDefault();
                        ImGui.Text(s.Value);
                        ImGui.TableSetColumnIndex(2);
                        //ImGui.SameLine();
                        if (sbgm != null)
                        {
                            var rf = b.BgmFiles.IndexOf(sbgm != null ? sbgm.Label : BgmTable.DEFAULT_TRACK);
                            ImGui.SetNextItemWidth(-26);
                            ImGui.Combo("##" + s.Value, ref rf, b.BgmArray, b.BgmFiles.Count);

                            if (rf != b.BgmFiles.IndexOf(sbgm != null ? sbgm.Label : BgmTable.DEFAULT_TRACK))
                            {
                                if (sbgm == null)
                                    bgmlist.LineList["LineStage"].Add(new() { Kind = s.Value, Label = b.BgmFiles[rf] });
                                else
                                    bgmlist.LineList["LineStage"].First(x => x.Kind == s.Value).Label = b.BgmFiles[rf];
                                scn.Stage.RebuildMusicAreas = true;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("X##remove" + s))
                            {
                                bgmlist.LineList["LineStage"].Remove(bgmlist.LineList["LineStage"].First(x => x.Kind == s.Value));
                                scn.Stage.RebuildMusicAreas = true;
                            }
                            ImGui.SetItemTooltip("This will remove this music type from this stage");
                        }
                        else
                        {
                            if (ImGui.Button("Enable Area Type##" + s, new(-1, default)))
                            {
                                bgmlist.LineList["LineStage"].Add(new() { Kind = s.Value, Label = BgmTable.DEFAULT_TRACK });
                                scn.Stage.RebuildMusicAreas = true;
                            }
                        }

                    }
                }
                ImGui.EndTable();

                if (ImGui.Button("Remove stage from Bgm List", new(-1, default)))
                {
                    b.StageBgmList.Remove(bgmlist);
                }
            }
            else
            {
                ImGui.TextDisabled("This Stage isn't present in the Bgm List");
                if (bgmlist != null)
                {
                    if (ImGui.Button("Add stage to Bgm List", new(-1, default)))
                    {
                        bgmlist.LineList.Add("LineStage", new());
                    }
                }
                else if (bgmlist == null)
                {
                    if (ImGui.Button("Add stage to Bgm List", new(-1, default)))
                    {
                        b.StageBgmList.Add(new() { StageName = scn.Stage.Name, Scenario = scn.Stage.Scenario, LineList = new() { { "LineStage", new() } } });
                    }
                }
            }

        }
        else
        {
            ImGui.TextDisabled("BgmTable.szs not found!");
        }
    }
    void FogAreasTab(Scene scn, float prevW, ImGuiStylePtr style)
    {

        ImGui.SetWindowFontScale(1.20f);
        ImGui.Text("Stage Fogs:");
        ImGui.Separator();
        ImGui.SetWindowFontScale(1f);
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

            ImGui.SetWindowFontScale(1.20f);
            if (scn.Stage.StageFogs[selectedfog].AreaId == -1)
                ImGui.Text("Main Fog:");
            else
                ImGui.Text("Fog " + scn.Stage.StageFogs[selectedfog].AreaId.ToString() + ":");
            ImGui.Separator();
            ImGui.SetWindowFontScale(1f);
            bool _disabled = selectedfog == 0;
            if (_disabled)
                ImGui.BeginDisabled();
            if (ImGui.Button("-", new(ImGui.GetWindowWidth() / 3 - 8, default)))
            {
                scn.RemoveFogAt(selectedfog);
                selectedfog -= 1;
            }
            if (_disabled)
                ImGui.EndDisabled();
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button("Duplicate", new(ImGui.GetWindowWidth() / 3 - 8, default)))
            {
                scn.DuplicateFog(selectedfog);
                selectedfog = scn.CountFogs() - 1;
            }
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button("+", new(ImGui.GetWindowWidth() / 3 - 8, default)))
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
        if (ImGui.BeginListBox("##LightList"))
        {
            bool iscliked = false;
            if (ImGui.Selectable("Map Object Light", lightIdx == 0))
            {
                lightIdx = 0;
                iscliked = true;
            }
            if (ImGui.Selectable("Object Light", lightIdx == 1))
            {
                lightIdx = 1;
                iscliked = true;
            }
            if (ImGui.Selectable("Player Light", lightIdx == 2))
            {
                lightIdx = 2;
                iscliked = true;
            }
            if (ImGui.Selectable("Stage Map Light", lightIdx == 3))
            {
                lightIdx = 3;
                iscliked = true;
            }

            if (iscliked && ImGui.IsKeyDown(ImGuiKey.ModShift))
                lightIdx = -1;
            ImGui.EndListBox();
        }
        selectedlight = lightIdx switch
        {
            0 => scn.Stage.LightParams.MapObjectLight,
            1 => scn.Stage.LightParams.ObjectLight,
            2 => scn.Stage.LightParams.PlayerLight,
            3 => scn.Stage.LightParams.StageMapLight,
            _ => null,
        };
        ImGui.PushItemWidth(prevW - PROP_WIDTH);
        if (scn.PreviewLight != selectedlight)
        {
            scn.PreviewLight = selectedlight;
        }
        if (selectedlight != null)
        {
            if (ImGui.Button("Copy to:"))
            {
                switch (copyLight)
                {
                    case 0:
                        scn.Stage.LightParams.MapObjectLight = new(selectedlight);
                        break;
                    case 1:
                        scn.Stage.LightParams.ObjectLight = new(selectedlight)!;
                        break;
                    case 2:
                        scn.Stage.LightParams.PlayerLight = new(selectedlight)!;
                        break;
                    case 3:
                        scn.Stage.LightParams.StageMapLight = new(selectedlight)!;
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
            ImGui.Checkbox("##Follow Camera", ref selectedlight.IsCameraFollow);

            ImGuiWidgets.PrePropertyWidthName("Direction", A, B);
            ImGui.DragFloat3("##Direction", ref selectedlight.Direction, 0.01f);

            ImGuiWidgets.PrePropertyWidthName("Ambient", A, B);
            ImGui.ColorEdit4("##Ambient", ref selectedlight.Ambient, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Diffuse", A, B);
            ImGui.ColorEdit4("##Diffuse", ref selectedlight.Diffuse, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Specular 0", A, B);
            ImGui.ColorEdit4("##Specular 0", ref selectedlight.Specular0, _colorEditFlags);

            ImGuiWidgets.PrePropertyWidthName("Specular 1", A, B);
            ImGui.ColorEdit4("##Specular 1", ref selectedlight.Specular1, _colorEditFlags);
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
            if (selectedlight.ConstantColors[5] != null)
            {


                Vector4 ColorEdit = (Vector4)selectedlight.ConstantColors[5]!;

                ImGui.Text("Constant 5");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Constant 5", 22, 30) - 24 * window.ScalingFactor);
                ImGui.ColorEdit4("##Constant 5", ref ColorEdit, _colorEditFlags);
                if (ColorEdit != selectedlight.ConstantColors[5])
                {
                    selectedlight.ConstantColors[5] = ColorEdit;
                }
                ImGui.SameLine();
                if (ImGui.Button($"X##5"))
                {
                    selectedlight.ConstantColors[5] = null;
                }
            }
            else
            {
                if (ImGui.Button("Add Constant 5"))
                {
                    selectedlight.ConstantColors[5] = new();
                }
            }

        }
        ImGui.PopItemWidth();
    }
    void LightAreaTab(Scene scn, float prevW, ImGuiStylePtr style)
    {

        ImGui.SetWindowFontScale(1.20f);
        ImGui.Text("Light Areas:");
        ImGui.Separator();
        ImGui.SetWindowFontScale(1f);
        if (ImGui.BeginTable("LAreaSelect", 2, _stageTableFlags,

        new(default, ImGui.GetWindowHeight() / 3.2f / window.ScalingFactor)))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Area Id", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 1f);
            ImGui.TableHeadersRow();
            if (scn.Stage.LightAreaNames != null && scn.Stage.LightAreaNames.Count > 0)
            {
                for (int _i = 0; _i < scn.Stage.LightAreaNames.Count; _i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushID("lightarea" + scn.Stage.LightAreaNames.Keys.ElementAt(_i));
                    var st = scn.Stage.LightAreaNames.Keys.ElementAt(_i).ToString();

                    if (ImGui.Selectable(st, _i == selectedlightarea, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        selectedlightarea = _i;
                    }

                    ImGui.PopID();
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(_i)]);

                }
            }
        }
        ImGui.EndTable();
        bool _disabled = selectedlightarea < 0 || selectedlightarea > scn.Stage.LightAreaNames.Count;

        if (!_disabled)
        {
            ImGui.SetWindowFontScale(1.20f);
            ImGui.Text("Light Area " + scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea) + ":");
            ImGui.Separator();
            ImGui.SetWindowFontScale(1f);
        }

        if (_disabled)
            ImGui.BeginDisabled();
        if (ImGui.Button("-", new(ImGui.GetWindowWidth() / 2 - 10, default)))
        {
            scn.Stage.LightAreaNames.Remove(scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea));
        }
        ImGui.SameLine(default, style.ItemSpacing.X / 2);
        if (_disabled)
            ImGui.EndDisabled();
        if (ImGui.Button("+", new(ImGui.GetWindowWidth() / 2 - 10, default)))
        {
            scn.AddLight();
        }
        if (selectedlightarea > scn.Stage.LightAreaNames.Count - 1)
            selectedlightarea = -1;
        if (selectedlightarea > -1)
        {

            ImGui.PushItemWidth(prevW - PROP_WIDTH - 20);
            var ReadLightAreas = window.ContextHandler.FSHandler.ReadLightAreas();
            var refStr = scn.Stage.LightAreaNames![scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)];

            int aId = scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea);
            ImGui.InputInt("Area Id", ref aId);
            if (aId != scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea) && !scn.Stage.LightAreaNames.ContainsKey(aId))
            {
                string tmpS = scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)];
                scn.Stage.LightAreaNames.Remove(scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea));
                scn.Stage.LightAreaNames.Add(aId, tmpS);
            }

            if (ReadLightAreas != null)
            {

                selectedlightName = ReadLightAreas.Keys.ToList().IndexOf(refStr);

                var keyArray = ReadLightAreas.Keys.ToArray();

                ImGui.Combo("Light Name", ref selectedlightName, keyArray, ReadLightAreas.Keys.Count - 1);

                if (refStr != keyArray[selectedlightName])
                    scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)] = keyArray[selectedlightName];

                LightArea area = ReadLightAreas![scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)]];

                ImGui.BeginDisabled();
                ImGui.DragInt("InterpolateFrame", ref area.InterpolateFrame, 1);
                ImGui.InputText("Name", ref area.Name, 128);
                ImGui.PopItemWidth();
                ImGui.PushItemWidth(prevW - style.ItemSpacing.X);
                ImGui.EndDisabled();
                if (ImGui.BeginListBox("##LightList", new(prevW * window.ScalingFactor, 4 * ImGui.GetTextLineHeight() * window.ScalingFactor)))
                {
                    if (ImGui.Selectable("Map Object Light", lightIdx == 0))
                    {
                        lightIdx = 0;
                    }
                    if (ImGui.Selectable("Object Light", lightIdx == 1))
                    {
                        lightIdx = 1;
                    }
                    if (ImGui.Selectable("Player Light", lightIdx == 2))
                    {
                        lightIdx = 2;
                    }
                    ImGui.EndListBox();
                }
                selectedlight = lightIdx switch
                {
                    0 => area.MapObjectLight,
                    1 => area.ObjectLight,
                    2 => area.PlayerLight,
                    _ => null,
                };
                ImGui.PushItemWidth(prevW - PROP_WIDTH);
                ImGui.BeginDisabled();
                if (scn.PreviewLight != selectedlight)
                {
                    scn.PreviewLight = selectedlight;
                }
                if (selectedlight != null)
                {
                    int A = 22;
                    int B = 30;

                    ImGuiWidgets.PrePropertyWidthName("Follow Camera", A, B);
                    ImGui.Checkbox("##Follow Camera", ref selectedlight.IsCameraFollow);

                    ImGuiWidgets.PrePropertyWidthName("Direction", A, B);
                    ImGui.DragFloat3("##Direction", ref selectedlight.Direction, 0.01f);

                    ImGuiWidgets.PrePropertyWidthName("Ambient", A, B);
                    ImGui.ColorEdit4("##Ambient", ref selectedlight.Ambient, _colorEditFlags);

                    ImGuiWidgets.PrePropertyWidthName("Diffuse", A, B);
                    ImGui.ColorEdit4("##Diffuse", ref selectedlight.Diffuse, _colorEditFlags);

                    ImGuiWidgets.PrePropertyWidthName("Specular 0", A, B);
                    ImGui.ColorEdit4("##Specular 0", ref selectedlight.Specular0, _colorEditFlags);

                    ImGuiWidgets.PrePropertyWidthName("Specular 1", A, B);
                    ImGui.ColorEdit4("##Specular 1", ref selectedlight.Specular1, _colorEditFlags);
                    for (int i = 0; i < 6; i++)
                    {
                        if (selectedlight.ConstantColors[i] != null)
                        {
                            Vector4 ColorEdit = (Vector4)selectedlight.ConstantColors[i]!;
                            ImGuiWidgets.PrePropertyWidthName($"Constant {i}", A, B);
                            ImGui.ColorEdit4($"Constant {i}", ref ColorEdit, _colorEditFlags);
                            if (ColorEdit != selectedlight.ConstantColors[i])
                            {
                                selectedlight.ConstantColors[i] = ColorEdit;
                            }
                        }
                    }

                }
                ImGui.EndDisabled();
                ImGui.PopItemWidth();
            }
            else
            {
                var ln = scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)];
                ImGui.InputText("Light Name", ref ln, 128);
                if (ln != scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)])
                    scn.Stage.LightAreaNames[scn.Stage.LightAreaNames.Keys.ElementAt(selectedlightarea)] = ln;
                ImGui.TextDisabled("LightDataArea.szs not found! \nCan't display Light info.");
            }
        }
    }
}
