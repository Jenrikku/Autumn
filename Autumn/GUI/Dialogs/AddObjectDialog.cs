using System.Diagnostics;
using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;
using Silk.NET.OpenGL;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// Dialog window that displays all possible actors, areas and rails to add to a level
/// </summary>
/// <param name="window"></param>
internal class AddObjectDialog(MainWindowContext window)
{
    private bool _isOpened = false;

    private string _name = "";
    private string _class = "";
    private string _searchQuery = "";
    private int[] _args = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
    private int[] _switch = [-1, -1, -1, -1, -1];
    private int _objectType = 0;
    private string[] _objectTypeNames = ["Object", "Goal", "StartEvent", "Start", "Demo"];
    private int _areaType = 0;
    readonly string[] _areaTypeNames = ["Area", "CameraArea"];
    readonly string[] _switches = ["A", "B", "Appear", "DeadOn", "Kill"];
    readonly string[] _switchTypes = ["None", "Read", "Write"];
    int _priority = 0;
    int _shape = 0;

    int _railShape = 0;
    int _railType = 0;
    readonly string[] _railShapeDesc = ["Line", "Circle (4)", "Circle (Any)", "Rectangle"];
    bool _railClosed = false;
    float _railCenterDistance = 1.0f;
    float _railPointDistance = 0.5f;
    float _railRectW = 3f;
    float _railRectL = 2f;
    int _railPointCount = 4;
    bool _railAuto = false;


    private const ImGuiTableFlags _newObjectClassTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    private int _selectedTab = -1;
    private bool _useClassName = false;
    public void Open()
    {
        _isOpened = true;
        _selectedTab = -1;
        ResetArgs(null);
        _switch = [-1, -1, -1, -1, -1];
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && !ImGui.GetIO().WantTextInput) // prevent exiting when input is focused
        {
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("Add New Object");

        Vector2 dimensions = new(800, 550);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Appearing);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Add New Object",
                ref _isOpened,
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse
            )
        )
            return;

        _useClassName = window.ContextHandler.Settings.UseClassNames;

        var obj = 0;
        var pvw = ImGui.GetWindowWidth();
        var pvh = ImGui.GetWindowHeight();
        var style = ImGui.GetStyle();
        if (ImGui.BeginTabBar("ObjectType"))
        {
            //ImGui.PushStyleColor(ImGuiCol.ChildBg, 0x6f0000ff);
            if (ImGui.BeginTabItem("Object"))
            {
                ObjectAreaTab(obj, pvw, pvh, style);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Area"))
            {
                ObjectAreaTab(obj, pvw, pvh, style, true);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Rail"))
            {
                //ObjectAreaTab(obj, pvw, pvh, style, ImGui.IsKeyDown(ImGuiKey.RightArrow));
                //ImGui.Text("Currently unsupported");
                RailTab(pvw, pvh, style);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
            //ImGui.PopStyleColor();
        }
        ImGui.EndPopup();
    }

    private void ObjectAreaTab(int obj, float pvw, float pvh, ImGuiStylePtr style, bool isArea = false)
    {
        bool databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
            _class,
            out ClassDatabaseWrapper.DatabaseEntry dbEntry
        );
        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(pvw / 2 - style.ItemSpacing.X * 4 - ImGui.CalcTextSize("Search:").X);
        if (_selectedTab == -1)
        {
            ImGui.SetKeyboardFocusHere();
        }
        ImGui.InputTextWithHint("##Search", "Name, Archive or ClassName", ref _searchQuery, 128);

        if (_selectedTab != (isArea ? 1 : 0))
        {
            _name = "";
            _class = "";
            _selectedTab = isArea ? 1 : 0;
        }
        ImGui.SameLine();
        ImGui.Text("Type:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(pvw / 2 - style.ItemSpacing.X - ImGui.CalcTextSize("Type:").X);
        if (isArea)
        {
            ImGui.Combo(
                "##Type",
                ref _areaType,
                _areaTypeNames,
                _areaTypeNames.Length
            );
        }
        else
        {
            ImGui.Combo(
                "##Type",
                ref _objectType,
                _objectTypeNames,
                _objectTypeNames.Length
            );
        }
        if (
            ImGui.BeginTable(
                "ClassTable",
                2,
                _newObjectClassTableFlags,
                new Vector2(pvw / 2 - style.ItemSpacing.X, pvh - 188 + 35 * window.ScalingFactor)
            )
        )
        {
            ImGui.TableSetupScrollFreeze(0, 1);

            if (_useClassName)
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableSetupColumn("ClassName", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableHeadersRow();
                foreach (var pair in ClassDatabaseWrapper.DatabaseEntries)
                {
                    if (
                        _searchQuery != string.Empty
                        && !pair.Key.ToLower().Contains(_searchQuery.ToLower())
                        && (pair.Value.Name == null
                        || !pair.Value.Name.Contains(_searchQuery.ToLower())
                        || !pair.Value.Name.Contains(_searchQuery.ToLower()))
                    )
                        continue;
                    if (isArea)
                    {
                        switch (_areaType)
                        {
                            case 0:
                                if (pair.Value.Type != "AreaObj")
                                    continue;
                                break;
                            case 1:
                                if (pair.Value.Type != "CameraAreaObj")
                                    continue;
                                break;
                        }
                    }
                    else
                    {
                        switch (_objectType)
                        {
                            case 0:
                                if (pair.Value.Type != null)
                                    continue;
                                break;
                            case 1:
                                if (pair.Value.Type != "GoalObj")
                                    continue;
                                break;
                            case 2:
                                if (pair.Value.Type != "StartEventObj")
                                    continue;
                                break;
                            case 3:
                                if (pair.Key != "Mario" && pair.Key != "Luigi")
                                    continue;
                                break;
                            case 4:
                                if (pair.Value.Type != "Demo")
                                    continue;
                                break;
                        }
                    }

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(1);
                    if (ImGui.Selectable(pair.Key + "##" + obj, false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _class = pair.Key;
                        databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                            _class,
                            out dbEntry
                        );
                        ResetArgs(dbEntry);
                    }
                    ImGui.TableSetColumnIndex(0);
                    if (pair.Value.Name != null) ImGui.Text(pair.Value.Name);
                    else ImGui.TextDisabled("Undocumented");
                    obj++;
                }
            }
            else
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.PreferSortDescending, 0.5f);
                ImGui.TableSetupColumn("Archive Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableHeadersRow();
                var ccnt = window.ContextHandler.FSHandler.ReadCreatorClassNameTable();
                var DBE = ClassDatabaseWrapper.DatabaseEntries;
                foreach (string k in ccnt.Keys)
                {
                    if (_searchQuery != string.Empty)
                        Debug.Assert(true);
                    if (
                        _searchQuery != string.Empty
                        && !k.ToLower().Contains(_searchQuery, StringComparison.InvariantCultureIgnoreCase)
                        && !ccnt[k].Contains(_searchQuery, StringComparison.InvariantCultureIgnoreCase)
                        && (!ClassDatabaseWrapper.DatabaseEntries.ContainsKey(ccnt[k]) || ClassDatabaseWrapper.DatabaseEntries[ccnt[k]].Name == null || !ClassDatabaseWrapper.DatabaseEntries[ccnt[k]].Name.Contains(_searchQuery, StringComparison.InvariantCultureIgnoreCase))
                    )
                        continue;
                    if (isArea)
                    {

                        switch (_areaType)
                        {
                            case 0:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    continue;
                                if (DBE[ccnt[k]].Type != "AreaObj")
                                    continue;
                                break;
                            case 1:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    continue;
                                if (DBE[ccnt[k]].Type != "CameraAreaObj")
                                    continue;
                                break;
                        }
                    }
                    else
                    {
                        switch (_objectType)
                        {
                            case 0:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    break;
                                if (DBE[ccnt[k]].Type != null)
                                    continue;
                                break;
                            case 1:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    continue;
                                if (DBE[ccnt[k]].Type != "GoalObj")
                                    continue;
                                break;
                            case 2:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    continue;
                                if (DBE[ccnt[k]].Type != "StartEventObj")
                                    continue;
                                break;
                            case 3:
                                if (k != "Mario" && k != "Luigi")
                                    continue;
                                break;
                            case 4:
                                if (!DBE.ContainsKey(ccnt[k]))
                                    continue;
                                if (DBE[ccnt[k]].Type != "Demo")
                                    continue;
                                break;
                        }
                    }

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(1);
                    bool useName = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(ccnt[k]) && ClassDatabaseWrapper.DatabaseEntries[ccnt[k]].Name != null;
                    if (ImGui.Selectable($"{k}##{obj}", false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _class = ccnt[k];
                        _name = k;
                        databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                            _class,
                            out dbEntry
                        );
                        ResetArgs(dbEntry);
                    }
                    ImGui.TableSetColumnIndex(0);
                    if (useName) ImGui.Text(ClassDatabaseWrapper.DatabaseEntries[ccnt[k]].Name);
                    else ImGui.TextDisabled($"Undocumented ({ccnt[k]})");
                    obj++;
                }
            }
        }

        ImGui.EndTable();
        ImGui.SameLine();
        {
            ImGui.BeginChild("##Desc_Args", new Vector2(pvw / 2 - style.ItemSpacing.X, pvh - 185 + 33 * window.ScalingFactor));
            string description = dbEntry.Description ?? "No Description";
            if (dbEntry.DescriptionAdditional is not null)
                description += $"\n{dbEntry.DescriptionAdditional}";
            ImGui.SetWindowFontScale(1.3f);
            if (!_useClassName)
            {
                if (databaseHasEntry)
                    ImGui.Text(string.IsNullOrEmpty(dbEntry.Name) ? (string.IsNullOrEmpty(_name) ? "Unknown" : _name) : dbEntry.Name);
                else if (!string.IsNullOrEmpty(_class))
                    ImGui.Text(_class);
                else
                {
                    ImGui.TextDisabled("No Object Selected");
                }
            }
            else
            {
                if (databaseHasEntry)
                    ImGui.Text(string.IsNullOrEmpty(dbEntry.Name) ? "Unknown" : dbEntry.Name);
                else if (!string.IsNullOrEmpty(_class))
                    ImGui.Text(_class);
                else
                {
                    ImGui.TextDisabled("No Object Selected");
                }
            }
            if (!_useClassName)
                ImGui.SetItemTooltip(_name);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.SameLine();
            ImGui.TextDisabled(_class);

            ImGui.BeginChild(
                "##Description",
                new Vector2(pvw / 2 - style.ItemSpacing.X * 2, 82 + 34 * window.ScalingFactor),
                ImGuiChildFlags.Border
            );
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (description == "No Description")
                ImGui.TextDisabled(description);
            else
                ImGui.TextWrapped(description);
            ImGui.EndChild();
            if (ImGui.BeginTabBar("argswitch"))
            {
                Vector2 table = new Vector2(pvw / 2 - style.ItemSpacing.X * 2 - 4, pvh - (isArea ? 355 : 300) / window.ScalingFactor - 28);
                if (ImGui.BeginTabItem("Args"))
                {
                    if (
                        ImGui.BeginTable(
                            "ArgTable",
                            4,
                            _newObjectClassTableFlags, table
                        )
                    )
                    {
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("Arg", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
                        ImGui.TableSetupColumn("Name");
                        ImGui.TableSetupColumn("Value");
                        ImGui.TableHeadersRow();
                        var m = isArea ? 8 : _objectType == 0 ? 10 : 8;
                        for (int i = 0; i < m; i++)
                        {
                            string arg = $"Arg{i}";
                            string name = "";
                            string argDescription = "";
                            string argType = "int";
                            if (databaseHasEntry
                                && dbEntry.Args is not null
                                && dbEntry.Args.TryGetValue(arg, out var argData))
                            {
                                if (argData.Name is not null)
                                    name = argData.Name;
                                if (argData.Type is not null)
                                    argType = argData.Type;
                                if (argData.Description is not null)
                                    argDescription = argData.Description;
                            }
                            else continue;

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text($"{i}");
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(argType);
                            ImGui.TableSetColumnIndex(2);
                            if (string.IsNullOrEmpty(name)) ImGui.TextDisabled("Unknown");
                            else ImGui.Text(name);
                            if (!string.IsNullOrEmpty(argDescription)) ImGui.SetItemTooltip(argDescription);
                            ImGui.TableSetColumnIndex(3);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.InputInt($"##{arg}input", ref _args[i]);
                            if (!string.IsNullOrEmpty(argDescription)) ImGui.SetItemTooltip(argDescription);
                        }

                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Switches"))
                {

                    if (
                        ImGui.BeginTable(
                            "SwitchTable",
                            3,
                    _newObjectClassTableFlags, table
                    ))
                    {
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("Switch", ImGuiTableColumnFlags.None, 0.3f);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.4f);
                        ImGui.TableSetupColumn("Value");
                        ImGui.TableHeadersRow();
                        int sw = 0;
                        foreach (string swn in _switches)
                        {
                            string swName = $"Switch{swn}";
                            string swDescription = "";
                            string swType = "";

                            if (dbEntry.Switches != null && dbEntry.Switches.ContainsKey(swName) && dbEntry.Switches[swName] != null)
                            {
                                swDescription = dbEntry.Switches[swName]!.Value.Description;
                                swType = dbEntry.Switches[swName]!.Value.Type;
                            }
                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(swn);
                            ImGui.TableSetColumnIndex(1);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            int swi = Array.IndexOf(_switchTypes, string.IsNullOrWhiteSpace(swType) ? "None" : swType);
                            ImGui.Text(string.IsNullOrWhiteSpace(swType) ? "None" : swType);
                            ImGui.TableSetColumnIndex(2);
                            if (!string.IsNullOrWhiteSpace(swDescription)) ImGui.SetItemTooltip(swDescription);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.InputInt($"##switch{swn} changer", ref _switch[sw]);
                            if (!string.IsNullOrWhiteSpace(swDescription)) ImGui.SetItemTooltip(swDescription);
                            sw += 1;
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            if (isArea)
            {
                ImGui.Text("Priority:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.CalcTextSize("Priority:").X - style.ItemSpacing.X * 2);
                ImGui.InputInt("##Priority1", ref _priority);


                ImGui.Text("Shape:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.CalcTextSize("Shape:").X - style.ItemSpacing.X * 2);
                ImGui.Combo("##Shape1", ref _shape, ["Cube", "Sphere", "Cylinder"], 3);
            }
            ImGui.EndChild();
        }

        float width = ImGui.GetContentRegionAvail().X;
        float spacingX = ImGui.GetStyle().ItemSpacing.X;
        float paddingX = ImGui.GetStyle().FramePadding.X;
        string txtName;
        if (isArea)
            txtName = "AreaName";
        else
            txtName = "ObjectName";
        ImGui.SetNextItemWidth(pvw / 2 - style.ItemSpacing.X * 2 - ImGui.CalcTextSize("<-").X / 2);
        ImGuiWidgets.InputTextRedWhenEmpty("##ObjectName", ref _name, 128, txtName);
        ImGui.SetItemTooltip(txtName);

        ImGui.SameLine();
        if (!_useClassName)
            ImGui.BeginDisabled();
        if (ImGuiWidgets.ArrowButton("l", ImGuiDir.Left))
            _name = _class;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(pvw / 2 - style.ItemSpacing.X * 4 - ImGui.CalcTextSize("<-").X / 2 + 5);
        if (ImGuiWidgets.InputTextRedWhenEmpty("##ClassName", ref _class, 128, "ClassName"))
            ResetArgs(dbEntry);
        ImGui.SetItemTooltip("ClassName");
        if (!_useClassName)
            ImGui.EndDisabled();

        bool canCreate = _name != string.Empty && (_useClassName ? (_class != string.Empty) : true);
        if (canCreate && ImGui.Button("Add", new(ImGui.GetWindowWidth() - style.ItemSpacing.X * 2, default)))
        {
            var _type = isArea ?
            _areaType switch
            {
                1 => StageObjType.CameraArea,
                _ => StageObjType.Area
            }
            : _objectType switch
            {
                1 => StageObjType.Goal,
                2 => StageObjType.StartEvent,
                3 => StageObjType.Start,
                4 => StageObjType.DemoScene,
                _ => StageObjType.Regular
            };
            if (!isArea)
                window.AddSceneMouseClickAction(new AddObjectAction(_name, _class, _args, _switch, _type).AddQueuedObject);
            else
                window.AddSceneMouseClickAction(new AddObjectAction(_name, _class, _args, _switch, _type, _priority, _shape).AddQueuedObject);
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }
    }

    private void RailTab(float pvw, float pvh, ImGuiStylePtr style)
    {
        //ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0,1,0,1));
        if (ImGui.BeginChild("LEFT", new(pvw / 2, pvh - 130)))
        {
            Vector2 rg = new (ImGui.GetContentRegionAvail().X / 2,ImGui.GetContentRegionAvail().Y / 2 - 10);

            if (ImGui.Button(_railShapeDesc[0], rg))
            {
                _railShape = 0;
                _railType = 0;
            }
            ImGui.SameLine(0, style.ItemInnerSpacing.X);
            if (ImGui.Button(_railShapeDesc[1], rg))
            {
                _railShape = 1;
                _railType = 1;
            }
            if (ImGui.Button(_railShapeDesc[2], rg))
            {
                _railShape = 2;
                _railType = 1;
            }
            ImGui.SameLine(0, style.ItemInnerSpacing.X);
            if (ImGui.Button(_railShapeDesc[3], rg))
            {
                _railShape = 3;
                _railType = 0;
            }

            ImGui.EndChild();
        }
        ImGui.SameLine();

        Vector2 v = ImGui.GetCursorPos();
        ImGui.SetWindowFontScale(1.3f);
        ImGui.Text(_railShapeDesc[_railShape]);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.SetCursorPosX(v.X);
        ImGui.SetCursorPosY(v.Y + 35);
        if (ImGui.BeginChild("RIGHT", new(pvw / 2- 25, pvh - 180), ImGuiChildFlags.Border))
        {
            int A = 10;
            int B = 20;
            ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Rail name", A, B));
            ImGui.InputText("##Name", ref _name, 128);
            if (_railShape == 0)
                _railClosed = false;
            else
            {
                ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Loop", A, B));
                ImGui.Checkbox("##Loop", ref _railClosed);
            }

            if (_railShape != 3)
            {
                ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Distance to center", A, B));
                if (ImGui.InputFloat("##DistCent", ref _railCenterDistance, 0.1f))
                    _railCenterDistance = float.Clamp(_railCenterDistance, 0.05f, 10);
            }
            if (_railShape == 1 || _railShape == 2)
            {
                if (_railShape != 0)
                {
                    ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Automatic handle distance", A, B));
                    ImGui.Checkbox("##AutoDist", ref _railAuto);
                }
                else
                    _railAuto = false;

                if (_railAuto)
                    ImGui.BeginDisabled();
                ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Handle distance to point", A, B));
                if (ImGui.InputFloat("##Handle", ref _railPointDistance, 0.1f))
                    _railPointDistance = float.Clamp(_railPointDistance, 0.0f, 2.0f);
                if (_railAuto)
                    ImGui.EndDisabled();
                if (_railShape == 2)
                {
                    ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Number of points", A, B));
                    ImGui.InputInt("##Points", ref _railPointCount, 1);
                    _railPointCount = int.Clamp(_railPointCount, 3, 25);
                }
            }
            else if (_railShape == 3)
            {
                ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Width", A, B));
                ImGui.InputFloat("##width", ref _railRectW, 0.1f);
                ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Length", A, B));
                ImGui.InputFloat("##length", ref _railRectL, 0.1f);
            }
            ImGui.SetNextItemWidth(ImGuiWidgets.PrePropertyWidthName("Rail type", A, B));
            ImGui.Combo("##railtype", ref _railType, ["Linear", "Bezier"], 2);
            ImGui.Text("TEST");
            ImGui.EndChild();
        }
        //ImGui.PopStyleColor();
        if (String.IsNullOrWhiteSpace(_name)) ImGui.BeginDisabled();
        if (ImGui.Button("OK", new(-1)))
        {
            RailPoint[] sent = _railShape switch
            {
                0 => RailDefaults.Line(_railCenterDistance),
                1 => RailDefaults.Circle(_railCenterDistance, _railAuto ? 1.0f / _railPointCount * 2 : _railPointDistance),
                2 => RailDefaults.Circle(_railPointCount, _railCenterDistance, _railAuto ? 1.0f / _railPointCount * 2 : _railPointDistance),
                3 => RailDefaults.Rectangle(_railRectW, _railRectL),
            };
            window.AddSceneMouseClickAction(new AddRailAction(_name, _args, sent,
            _railShape == 0 ? RailPointType.Linear : (_railType == 0 ? RailPointType.Linear : RailPointType.Bezier),
             _railClosed).AddQueuedRail);
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }
        if (String.IsNullOrWhiteSpace(_name)) ImGui.EndDisabled();
    }

    private void ResetArgs(ClassDatabaseWrapper.DatabaseEntry? dbEntry)
    {
        for (int i = 0; i < 10; i++)
            _args[i] = dbEntry != null && dbEntry.Value.Args != null && dbEntry.Value.Args.ContainsKey($"Arg{i}") ? (int)dbEntry.Value.Args[$"Arg{i}"].Default : -1;
    }

    public class AddObjectAction(string _name, string _class, int[] _args, int[] _sw, StageObjType _type, int _priority = -1, int _shape = 0)
    {
        static string[] _designList = ["LightArea", "FogAreaCameraPos", "FogArea"];
        static string[] _soundList = ["SoundEmitArea", "SoundEmitObj", "BgmChangeArea", "AudioEffectChangeArea", "AudioVolumeSettingArea"];
        public void AddQueuedObject(MainWindowContext window, Vector4 trans)
        {
            if (window.CurrentScene is null || window.GL is null)
                return;

            StageObj newObj = new()
            {
                Type = _type,
                Name = _name,
                ClassName = window.ContextHandler.Settings.UseClassNames ? _class : null,
                Translation = new(trans.X * 100, trans.Y * 100, trans.Z * 100),
                SwitchA = _sw[0],
                SwitchB = _sw[1],
                SwitchAppear = _sw[2],
                SwitchDeadOn = _sw[3],
                SwitchKill = _sw[4]
            };

            if (_designList.Contains(newObj.Name)) newObj.FileType = StageFileType.Design;
            else if (_soundList.Contains(newObj.Name)) newObj.FileType = StageFileType.Sound;
            else newObj.FileType = StageFileType.Map;

            // set up arguments
            int argNum = 10;
            if (newObj.Type == StageObjType.Area || newObj.Type == StageObjType.Goal) argNum = 8;
            else if (newObj.Type != StageObjType.Regular) argNum = 0;
            for (int i = 0; i < argNum; i++)
                newObj.Properties.Add($"Arg{i}", _args[i]);

            // set up extra properties
            if (newObj.Type == StageObjType.Area || newObj.Type == StageObjType.CameraArea)
            {
                newObj.Properties.Add("Priority", _priority);
                newObj.Properties.Add("ShapeModelNo", _shape);
            }
            else if (newObj.Type == StageObjType.Start)
                newObj.Properties.Add("MarioNo", 0);
            else if (newObj.Type == StageObjType.DemoScene)
            {
                newObj.Properties.Add("Action1", "-");
                newObj.Properties.Add("Action2", "-");
                newObj.Properties.Add("Action3", "-");
                newObj.Properties.Add("Action4", "-");
                newObj.Properties.Add("Action5", "-");
                newObj.Properties.Add("LuigiType", "Common");
                newObj.Properties.Add("MarioType", "Common");
                newObj.Properties.Add("ModelName", "-");
                newObj.Properties.Add("SuffixName", "-");
            }

            ChangeHandler.ChangeCreate(window, window.CurrentScene.History, newObj);

            if (window.Keyboard?.IsShiftPressed() ?? false)
                window.AddSceneMouseClickAction(AddQueuedObject);
        }
    }

    public class AddRailAction(string _name, int[] _args, RailPoint[] _points, RailPointType _type, bool _closed)
    {
        public void AddQueuedRail(MainWindowContext window, Vector4 trans)
        {
            if (window.CurrentScene is null || window.GL is null)
                return;

            RailObj newRail = new()
            {
                PointType = _type,
                Name = _name,
                Closed = _closed,
                Type = StageObjType.Rail
            };
            newRail.FileType = StageFileType.Map;

            for (int i = 0; i < 8; i++)
                newRail.Properties.Add($"Arg{i}", _args[i]);

            newRail.Properties.Add("MultiFileName", "Autumn");

            Vector3 off = new(trans.X * 100, trans.Y * 100, trans.Z * 100);
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i] *= 200;
                _points[i].Point0Trans += off;
                _points[i].Point1Trans += off;
                _points[i].Point2Trans += off;
                newRail.Points.Add(_points[i]);
            }

            ChangeHandler.ChangeCreate(window, window.CurrentScene.History, newRail);

            if (window.Keyboard?.IsShiftPressed() ?? false)
                window.AddSceneMouseClickAction(AddQueuedRail);
        }
    }
}
