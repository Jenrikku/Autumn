using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class MiscParamsWindow(MainWindowContext window)
{

    int musicIdx = 0;
    public bool _isOpen = false;
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
        
        if (!ImGui.Begin("General##MiscParams", ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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

        ImGuiWidgets.TextHeader("Stage Params:");

        ImGuiWidgets.InputInt("Timer", ref scn.Stage.StageParams.Timer, 10);
        ImGuiWidgets.InputInt("Restart", ref scn.Stage.StageParams.RestartTimer, 10);
        ImGuiWidgets.InputInt("MaxPow", ref scn.Stage.StageParams.MaxPowerUps, 1);
        ImGui.NewLine();
        ImGui.Separator();
        ImGuiWidgets.TextHeader("Stage Music:");
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
            ImGui.Text("Music Types:");
            ImGui.SameLine();
            ImGuiWidgets.HelpTooltip("Ids determine the music type to play on a BgmChangeArea");
            var bgmlist = b.StageBgmList.FirstOrDefault(x => x.StageName == scn.Stage.Name && x.Scenario == scn.Stage.Scenario);
            if (bgmlist == null)
                bgmlist = b.StageBgmList.FirstOrDefault(x => x.StageName == scn.Stage.Name && x.Scenario == null);
            if (bgmlist != null && bgmlist.LineList.ContainsKey("LineStage"))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2));
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(1));
                if (ImGui.BeginTable("MusicAreaIdTables", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                    ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 0.22f);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.7f);
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
                            if (ImGui.Button(IconUtils.MINUS+"##remove" + s))
                            {
                                bgmlist.LineList["LineStage"].Remove(bgmlist.LineList["LineStage"].First(x => x.Kind == s.Value));
                                scn.Stage.RebuildMusicAreas = true;
                            }
                            ImGui.SetItemTooltip("This will remove this music type from this stage");
                        }
                        else
                        {
                            if (ImGui.Button("Enable Area Type##" + s, new(-1, 25)))
                            {
                                bgmlist.LineList["LineStage"].Add(new() { Kind = s.Value, Label = BgmTable.DEFAULT_TRACK });
                                scn.Stage.RebuildMusicAreas = true;
                            }
                        }

                    }
                }
                ImGui.EndTable();
                ImGui.PopStyleVar(3);

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

        ImGui.End();
    }
}