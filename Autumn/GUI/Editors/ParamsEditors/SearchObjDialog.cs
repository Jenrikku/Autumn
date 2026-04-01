using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

/// <summary>
/// Dialog window that lets the user search for objects in the current stage
/// </summary>
/// <param name="window"></param>
internal class SearchWindow
{
    public SearchWindow(MainWindowContext _window)
    {
        window = _window;
        objTypes = [.. objTypes, .. Enum.GetNames<StageObjType>()];
    }
    private MainWindowContext window;
    public bool IsOpen = false;
    private string _name = "";
    private string _class = "";
    private string _layer = "";
    private StageCamera? _camera;
    private RailObj? _rail;
    private int _type = 0; // None / StageObjType +1 -> Regular = 1 instead of 0
    private int _vid = -1;
    private int _clipId = -1;
    private bool _switchAll = false;
    private int _switch = -1;
    private int[] _switches = [-1, -1, -1, -1, -1];
    private readonly int[] _baseswitches = [-1, -1, -1, -1, -1];
    HashSet<ISceneObj> _foundObjects = new();
    public string[] objTypes = ["Any"];

    private const float PROP_WIDTH = 105f;
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON }; // | ImGuiDockNodeFlags.NoUndocking };
    private bool updateCameras = true;
    private string[] cameraStrings = [];
    private List<StageCamera> cameraslinks = new();
    private bool searchRailOnly = false;
    private int _railpoints = 2;
    private bool _closed = false;

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

        if (!ImGui.Begin("Search##SearchWindow", ref IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
            return;
        if (window.CurrentScene == null)
        {
            ImGui.TextDisabled("Please load a stage first");
            ImGui.End();
            return;
        }


        var style = ImGui.GetStyle();
        float prevW = ImGui.GetContentRegionAvail().X;
        Scene scn = window.CurrentScene;
        if (updateCameras)
        {
            var cams = scn.Stage.CameraParams.Cameras;
            if (_type == 0) cameraslinks = cams;
            else if (_type == (int)StageObjType.CameraArea + 1) cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Map).ToList();
            // if (stageObj.Name == "EntranceCameraObj") cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Entrance).ToList();
            else if (_type == (int)StageObjType.DemoScene + 1) cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Event).ToList();
            else cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Object).ToList();

            cameraStrings = new string[cameraslinks.Count + 1];
            cameraStrings[0] = "No camera selected";
            for (int cs = 1; cs < cameraStrings.Length; cs++)
            {
                cameraStrings[cs] = cameraslinks[cs - 1].CameraName();
            }
            updateCameras = false;
        }
        
        
        ImGuiWidgets.TextHeader("Search:");
        if (ImGui.Button("Clear Search", new Vector2(-1, default)))
        {
            _name = "";
            _class = "";
            _layer = "";
            _camera = null;
            _rail = null;
            _type = 0; // None / StageObjType +1 -> Regular = 1 instead of 0
            _vid = -1;
            _clipId = -1;
            _switches = [-1, -1, -1, -1, -1];
            _switch = -1;
            _switchAll = false;
            _foundObjects.Clear();
        }
        bool UpdateS = false;
        ImGui.SeparatorText("By text:");
        ImGui.SetNextItemWidth(prevW);
        if (ImGui.InputTextWithHint("##SearchName", "Name", ref _name, 128))
            UpdateS = true;
        ImGui.SetNextItemWidth(prevW);
        if (ImGui.InputTextWithHint("##SearchClassName", "ClassName", ref _class, 128))
            UpdateS = true;
        ImGui.SetNextItemWidth(prevW);
        if (ImGui.InputTextWithHint("##SearchLayer", "Layer", ref _layer, 128))
            UpdateS = true;

        ImGui.SeparatorText("By values:");

        int rff = 0;
        if (_camera != null) rff = cameraslinks.IndexOf(_camera) + 1;
        int orff = rff;
        ImGuiWidgets.PrePropertyWidthName("Camera Id");
        ImGui.Combo("##CAMERASearch", ref rff, cameraStrings, cameraStrings.Length);
        if (rff != orff)
        {
            if (rff == 0) _camera = null;
            else _camera = cameraslinks[rff - 1];
            UpdateS = true;
        }


        var rails = scn!.EnumerateRailSceneObjs();
        string[] railStrings = new string[rails.Count() + 1];
        int bbbb = 1;
        railStrings[0] = "No rail selected";
        foreach (RailSceneObj rail in rails)
        {
            railStrings[bbbb] = rail.RailObj.Name;
            bbbb += 1;
        }
        int rfrail = 0;
        if (_rail is not null)
        {
            var rals = rails.FirstOrDefault(x => x.RailObj.Name == _rail.Name);
            if (rals != null)
                rfrail = rails.ToList().IndexOf(rals) + 1;
        }
        int rfr2 = rfrail;
        ImGuiWidgets.PrePropertyWidthName("Rail");

        ImGui.Combo("##SearchRailselector", ref rfr2, railStrings, rails.Count() + 1);
        if (rfr2 != rfrail)
        {
            if (rfr2 > 0)
            {
                _rail = rails.ElementAt(rfr2 - 1).RailObj;
            }
            else _rail = null;
            UpdateS = true;
        }
        
        ImGuiWidgets.PrePropertyWidthName("View Id");
        if (ImGui.InputInt("##SearchViewId", ref _vid, 1)) UpdateS = true;
        ImGuiWidgets.PrePropertyWidthName("Clipping Group Id");
        if (ImGui.InputInt("##ClippingGroupId", ref _clipId, 1)) UpdateS = true;
        
        if (ImGui.CollapsingHeader("Rail Specific"))
        {

            ImGuiWidgets.PrePropertyWidthName("Search Rails only");
            if (ImGui.Checkbox("##SearchRails", ref searchRailOnly)) UpdateS = true;
            if (!searchRailOnly) ImGui.BeginDisabled();
            ImGuiWidgets.PrePropertyWidthName("Closed loop");
            if (ImGui.Checkbox("##SearchClose", ref _closed)) UpdateS = true;
            ImGuiWidgets.PrePropertyWidthName("Number of points");
            if (ImGui.InputInt("##SearchNoPt", ref _railpoints)) UpdateS = true;
            if (!searchRailOnly) ImGui.EndDisabled();
        }



        ImGui.SeparatorText("By switches:");
        if (ImGui.Checkbox("Search by id", ref _switchAll)) UpdateS = true;
        if (!_switchAll) ImGui.BeginDisabled();
        ImGuiWidgets.PrePropertyWidthName("Switch");
        if (ImGui.InputInt("##SearchSwitch", ref _switch, 1)) UpdateS = true;
        if (!_switchAll) ImGui.EndDisabled();
        if (_switchAll) ImGui.BeginDisabled();
        ImGuiWidgets.PrePropertyWidthName("Switch A");
        if (ImGui.InputInt("##SearchSwitchA", ref _switches[0], 1)) UpdateS = true;
        ImGuiWidgets.PrePropertyWidthName("Switch B");
        if (ImGui.InputInt("##SearchSwitchB", ref _switches[1], 1)) UpdateS = true;
        ImGuiWidgets.PrePropertyWidthName("Switch Appear");
        if (ImGui.InputInt("##SearchSwitchAppear", ref _switches[2], 1)) UpdateS = true;
        ImGuiWidgets.PrePropertyWidthName("Switch DeadOn");
        if (ImGui.InputInt("##SearchSwitchDeadOn", ref _switches[3], 1)) UpdateS = true;
        ImGuiWidgets.PrePropertyWidthName("Switch Kill");
        if (ImGui.InputInt("##SearchSwitchKill", ref _switches[4], 1)) UpdateS = true;
        if (_switchAll) ImGui.EndDisabled();

        // Check specific properties / values in the future -> ArgX, MultiFileName...

        if (UpdateS) 
            UpdateSearch(scn);
            
        int obj = 0;
        ImGui.SeparatorText("Search results:");
        ImGui.SetNextItemWidth(prevW);

        if (ImGui.BeginTable("searchTable", 3,
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersOuter
            | ImGuiTableFlags.BordersV
            | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.None);
            ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 3.0f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None);
            ImGui.TableHeadersRow();
            //if (autoResize) ImGui.SetScrollY(0);
            int cidx = 0;
            if (_foundObjects.Count > 0)
            foreach (ISceneObj ch in _foundObjects)
            {
                string name = "";
                string tp = "";
                if (ch is not RailSceneObj && ch is not IStageSceneObj) continue;
                if (ch is RailSceneObj ro) 
                {
                    name = ro.RailObj.Name;
                    tp = "Rail"; 
                }
                else
                {
                    name = (ch as IStageSceneObj)!.StageObj.Name;
                    tp = (ch as IStageSceneObj)!.StageObj.Type.ToString();
                }
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(1);

                ImGui.PushID("SearchSelectable" + cidx);
                if (ImGui.Selectable(name, false, ImGuiSelectableFlags.None, new(ImGui.GetColumnWidth(), 30)))
                {
                    ChangeHandler.ToggleObjectSelection(window, scn.History,
                        ch.PickingId, !window.Keyboard?.IsCtrlPressed() ?? true);
                    window.CameraToObject(ch);
                }

                ImGui.TableSetColumnIndex(2);

                ImGui.Text(tp);

                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("SearchView" + cidx);
                if (ImGuiWidgets.HoverButton(IconUtils.MAG_GLASS, new(ImGui.GetColumnWidth(), 30)))
                {
                    window.CameraToObject(ch);
                }

                cidx++;
            }

            ImGui.EndTable();
            ImGui.Spacing();
        }

        ImGui.End();
    }

    void UpdateSearch(Scene scn)
    {
        // If no search filters, show no results
        _foundObjects.Clear();
        if (String.IsNullOrWhiteSpace(_name) &&
        String.IsNullOrWhiteSpace(_class) &&
        String.IsNullOrWhiteSpace(_layer) &&
        _camera is null && 
        _rail is null && 
        _type == 0 &&
        _vid == -1 &&
        _clipId == -1 &&
        _switches == _baseswitches)
        {
            return;
        }
        bool add;
        bool remove;
        if (!searchRailOnly)
        foreach (ISceneObj sco in scn.EnumerateSceneObjs())
        {
            add = false;
            remove = false;
            if (sco is RailSceneObj ro)
            {
                if (!String.IsNullOrWhiteSpace(_name))
                {
                    if (ro.RailObj.Name.Contains(_name, StringComparison.InvariantCultureIgnoreCase)) add = true;
                    else remove = true;
                }
                
                if (!String.IsNullOrWhiteSpace(_layer))
                {
                    if (ro.RailObj.Layer.Contains(_layer, StringComparison.InvariantCultureIgnoreCase)) add = true;
                    else remove = true;
                }
                
            }
            else if (sco is IStageSceneObj st)
            {
                if (!String.IsNullOrWhiteSpace(_name))
                {
                    if (st.StageObj.Name.Contains(_name, StringComparison.InvariantCultureIgnoreCase)) add = true;
                    else remove = true;
                }
                if (!String.IsNullOrWhiteSpace(_class))
                {
                    if (GetClassFromCCNT(st.StageObj.Name)?.Contains(_class, StringComparison.InvariantCultureIgnoreCase) ?? false) add = true;
                    else remove = true;
                }
                if (!String.IsNullOrWhiteSpace(_layer))
                {
                    if (st.StageObj.Layer.Contains(_layer, StringComparison.InvariantCultureIgnoreCase)) add = true;
                    else remove = true;
                }
                
                if (_vid != -1)
                {
                    if (st.StageObj.Name == "ViewCtrlArea" && st.StageObj.Properties.ContainsKey("Arg0"))
                    {
                        if ((int)st.StageObj.Properties["Arg0"]! == _vid) add = true;
                        else remove = true;
                    }
                    else
                    {
                        if ( st.StageObj.ViewId == _vid) add = true;
                        else remove = true;
                    }
                }
                if (_clipId != -1)
                {
                    if ( st.StageObj.ClippingGroupId == _clipId) add = true;
                    else remove = true;
                }
                if (_rail is not null)
                {
                    if (st.StageObj.Rail is not null && st.StageObj.Rail == _rail) add = true;
                    else remove = true;
                }
                if (_camera is not null)
                {
                    if (st.StageObj.CameraId == _camera.UserGroupId) add = true;
                    else remove = true;
                }

                if (_switchAll)
                {
                    if (_switch != -1)
                    {
                        if (st.StageObj.SwitchA == _switch ||
                            st.StageObj.SwitchB == _switch ||
                            st.StageObj.SwitchAppear == _switch ||
                            st.StageObj.SwitchDeadOn == _switch ||
                            st.StageObj.SwitchKill == _switch) add = true;
                        else remove = true;
                    }
                }
                else
                {
                    if (_switches[0] != -1)
                    {
                        if (st.StageObj.SwitchA == _switches[0]) add = true;
                        else remove = true;
                    }
                    if (_switches[1] != -1)
                    {
                        if (st.StageObj.SwitchB == _switches[1]) add = true;
                        else remove = true;
                    }
                    if (_switches[2] != -1)
                    {
                        if (st.StageObj.SwitchAppear == _switches[2]) add = true;
                        else remove = true;
                    }
                    if (_switches[3] != -1)
                    {
                        if (st.StageObj.SwitchDeadOn == _switches[3]) add = true;
                        else remove = true;
                    }
                    if (_switches[4] != -1)
                    {
                        if (st.StageObj.SwitchKill == _switches[4]) add = true;
                        else remove = true;
                    }
                }
            }
            if (!remove && add) _foundObjects.Add(sco);
        }
        else
        foreach (RailSceneObj ro in scn.EnumerateRailSceneObjs())
        {
            add = false;
            remove = false;
            if (!String.IsNullOrWhiteSpace(_name))
            {
                if (ro.RailObj.Name.Contains(_name, StringComparison.CurrentCultureIgnoreCase)) add = true;
                else remove = true;
            }
            if (!String.IsNullOrWhiteSpace(_layer))
            {
                if (ro.RailObj.Layer.Contains(_layer, StringComparison.CurrentCultureIgnoreCase)) add = true;
                else remove = true;
            }
            if (ro.RailObj.Closed == _closed) add = true;
            else remove = true;
            if (ro.RailObj.Points.Count == _railpoints) add = true;
            else remove = true;
            if (!remove && add) _foundObjects.Add(ro);
        }

        
        // Ignore all fields that are empty or have no value
        // Console.WriteLine("Updated.");
    }
    private string? GetClassFromCCNT(string objectName)
    {
        var table = window.ContextHandler.FSHandler.ReadCreatorClassNameTable();

        if (!table.TryGetValue(objectName, out string? className))
            return null;

        return className;
    }
}
