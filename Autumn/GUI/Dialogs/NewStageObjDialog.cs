using System.Diagnostics;
using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

internal class NewStageObjDialog(MainWindowContext window)
{
    private bool _isOpened = false;

    private string _name = "";
    private string _class = "";
    private string _searchQuery = "";
    private bool _prevClassValid = false;
    private int[] _args = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
    private int _objectType = 0;
    private string[] _objectTypeNames = ["Object", "Goal", "StartEvent", "Start", "Demo"];
    private int _areaType = 0;
    private string[] _areaTypeNames = ["Area", "CameraArea"];
    int _priority = 0;
    int _shape = 0;

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
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
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
                ImGui.Text("Currently unsupported");
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
                        _prevClassValid = databaseHasEntry;
                        databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                            _class,
                            out dbEntry
                        );
                        ResetArgs();
                    }
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(pair.Value.Name ?? "");
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

                    ImGui.TableSetColumnIndex(0);
                    if (ImGui.Selectable((ClassDatabaseWrapper.DatabaseEntries.ContainsKey(ccnt[k]) ? ClassDatabaseWrapper.DatabaseEntries[ccnt[k]].Name ?? "" : "") + "##" + obj, false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _class = ccnt[k];
                        _name = k;
                        _prevClassValid = databaseHasEntry;
                        databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                            _class,
                            out dbEntry
                        );
                        ResetArgs();
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(k);
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
            if (
                ImGui.BeginTable(
                    "ArgTable",
                    4,
                    _newObjectClassTableFlags,
                    new Vector2(pvw / 2 - style.ItemSpacing.X * 2, pvh - (isArea ? 355 : 300) / window.ScalingFactor)
                )
            )
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Arg", ImGuiTableColumnFlags.None, 0.2f);
                ImGui.TableSetupColumn("Val", ImGuiTableColumnFlags.None, 0.35f);
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Desc");
                ImGui.TableHeadersRow();
                var m = isArea ? 8 : _objectType == 0 ? 10 : 8;
                for (int i = 0; i < m; i++)
                {
                    string arg = $"Arg{i}";
                    string name = "";
                    string argDescription = "";
                    if (
                        databaseHasEntry
                        && dbEntry.Args is not null
                        && dbEntry.Args.TryGetValue(arg, out var argData)
                    )
                    {
                        if (argData.Name is not null)
                            name = argData.Name;
                        if (argData.Description is not null)
                            argDescription = argData.Description;
                        if (!_prevClassValid)
                            if (argData.Default is double)
                                _args[i] = Convert.ToInt32(argData.Default);
                            else
                                _args[i] = (int)argData.Default;
                    }

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(arg);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.DragInt($"##{arg}", ref _args[i]);
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(name);
                    ImGui.TableSetColumnIndex(3);
                    bool needScrollbar =
                        ImGui.CalcTextSize(argDescription).X
                        > ImGui.GetContentRegionAvail().X;
                    float ysize =
                        ImGui.GetFont().FontSize
                        * (ImGui.GetFont().Scale * (needScrollbar ? 1.8f : 1.0f));
                    ImGui.BeginChild(
                        $"##ArgDescription{i}",
                        new Vector2(0, ysize),
                        ImGuiChildFlags.None,
                        ImGuiWindowFlags.HorizontalScrollbar
                    );
                    ImGui.Text(argDescription);
                    ImGui.EndChild();
                }

                ImGui.EndTable();
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

        // float buttonTextSizeX = ImGui.CalcTextSize("<-").X;
        // float objectNameWidth =
        //     width * 0.5f - (paddingX * 2 + spacingX * 2 + buttonTextSizeX);
        //ImGui.PushItemWidth(objectNameWidth);
        //ImGui.PopItemWidth();
        ImGui.SameLine();
        if (!_useClassName)
            ImGui.BeginDisabled();
        if (ImGuiWidgets.ArrowButton("l", ImGuiDir.Left))
            _name = _class;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(pvw / 2 - style.ItemSpacing.X * 4 - ImGui.CalcTextSize("<-").X/2 + 5);
        if (ImGuiWidgets.InputTextRedWhenEmpty("##ClassName", ref _class, 128, "ClassName"))
            ResetArgs();
        ImGui.SetItemTooltip("ClassName");
        if (!_useClassName)
            ImGui.EndDisabled();

        //ImGui.SetNextItemWidth(100);
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
                window.AddSceneMouseClickAction(new AddObjectAction(_name, _class, _args, _type).AddQueuedObject);
            else
                window.AddSceneMouseClickAction(new AddObjectAction(_name, _class, _args, _type, _priority, _shape).AddQueuedObject);
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }
    }

    private void ResetArgs()
    {
        for (int i = 0; i < 10; i++)
            _args[i] = -1;
    }

    public class AddObjectAction(string _name, string _class, int[] _args, StageObjType _type, int _priority = -1, int _shape = 0)
    {
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
            };

	    List<string> DesignList = ["LightArea", "FogAreaCameraPos", "FogArea"];
            List<string> SoundList = ["SoundEmitArea", "SoundEmitObj", "BgmChangeArea", "AudioEffectChangeArea", "AudioVolumeSettingArea"];

            if (DesignList.Contains(newObj.Name)) newObj.FileType = StageFileType.Design;
            else if (SoundList.Contains(newObj.Name)) newObj.FileType = StageFileType.Sound;
            else newObj.FileType = StageFileType.Map;

            // set up arguments
            int argNum = 10;
            if (newObj.Type == StageObjType.Area || newObj.Type == StageObjType.Goal) argNum = 8;
            else if (newObj.Type != StageObjType.Regular) argNum = 0;
            for (int i = 0; i < argNum; i++)
                newObj.Properties.Add($"Arg{i}", _args[i]);

            // set up 
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
}
