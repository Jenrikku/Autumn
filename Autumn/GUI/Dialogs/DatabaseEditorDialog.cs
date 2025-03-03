using System.Numerics;
using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

internal class DatabaseEditor(MainWindowContext _window)
{
    private bool _isOpened = false;
    private SortedDictionary<string, ClassDatabaseWrapper.DatabaseEntry> _dbEntries;
    private int[] _args = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
    private int _argsel = -1;
    readonly string[] switches = ["A", "B", "Appear", "Kill", "DeadOn"];
    readonly string[] switchTypes = ["None", "Read", "Write"];
    readonly string[] argTypes = ["bool", "int", "enum"];
    readonly string[] actorTypes = ["Obj", "AreaObj", "CameraAreaObj", "GoalObj", "StartEventObj", "Demo"];
    readonly string[] filter = ["All", "Obj", "AreaObj", "CameraAreaObj", "GoalObj", "StartEventObj", "Demo"];
    private int _filterIdx = 0;
    private string _search = "";
    private List<string> _modifiedEntries = new();
    private ClassDatabaseWrapper.DatabaseEntry entry;
    Vector2 dimensions = new(742, 520);
    bool _argEdit = false;
    bool _isEditor = true;
    ImGuiTableFlags _tableFlags = ImGuiTableFlags.RowBg
                                | ImGuiTableFlags.BordersOuter
                                | ImGuiTableFlags.BordersV
                                | ImGuiTableFlags.ScrollY
                                | ImGuiTableFlags.Resizable;

    bool _editClassName = false;
    bool _editArgCanOverwrite = false;
    int _editArgId = 0; // 0 through 9
    int _editArgType = 1; // Bool, Int, Enum
    string _editArgDesc = "";
    string _editArgName = "";
    int _editArgDefault = -1;
    int? _editArgMin;
    int? _editArgMax;
    Dictionary<int, string> _editEnumValues = new();
    string _editEnNm = "";
    int _editEnVal = -1;
    bool setscroll = false;

    public void Open()
    {
        _isOpened = true;
        _dbEntries = new(ClassDatabaseWrapper.DatabaseEntries);
        _isEditor = _window.ContextHandler.SystemSettings.EnableDBEditor;
    }
    public void Reset()
    {
        dimensions = new Vector2(742, 520) * _window.ScalingFactor;
        entry = new();
        _modifiedEntries.Clear();
        _search = "";
    }
    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            if (_argEdit)
                _argEdit = false;
            else
            {
                if (!ImGui.GetIO().WantTextInput) // prevent exiting when input is focused
                {
                    Reset();
                    _isOpened = false;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.OpenPopup("Class Database Editor");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new(0.5f, 0.5f));

        if (
            !ImGui.BeginPopupModal(
                "Class Database Editor",
                ref _isOpened,
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar
            )
        )
            return;
        dimensions = ImGui.GetWindowSize();
        var style = ImGui.GetStyle();
        bool update = false;

        if (_argEdit)
        {
            ArgWindow(style);
        }
        if (_argEdit)
            ImGui.BeginDisabled();
        if (ImGui.BeginChild("LEFTSIDE", new(ImGui.GetContentRegionAvail().X * 18 / 30, ImGui.GetContentRegionAvail().Y - 30)))
        {
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X / 2);
            ImGui.InputTextWithHint("##SEARCHBOX", "Class or Documented name", ref _search, 100);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X / 2);
            ImGui.Combo("##Filter", ref _filterIdx, filter, filter.Length);
            var keys = _dbEntries.Keys.ToList();
            if (ImGui.BeginTable("ClassTable", 2,
                                    _tableFlags,
                                    new(ImGui.GetWindowWidth() - 1, ImGui.GetContentRegionAvail().Y - 30)))
            {
                int i = 0;
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                ImGui.TableSetupColumn("Documented Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableSetupColumn("Class Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableHeadersRow();
                foreach (string s in _dbEntries.Keys)
                {
                    if (!string.IsNullOrEmpty(_search)
                    && !s.Contains(_search, StringComparison.InvariantCultureIgnoreCase)
                    && (_dbEntries[s].Name == null
                    || !_dbEntries[s].Name!.Contains(_search, StringComparison.InvariantCultureIgnoreCase)))
                        continue;
                    switch (_filterIdx)
                    {
                        case 1:
                            if (_dbEntries[s].Type != null) continue;
                            break;
                        case 2:
                            if (_dbEntries[s].Type != "AreaObj") continue;
                            break;
                        case 3:
                            if (_dbEntries[s].Type != "CameraAreaObj") continue;
                            break;
                        case 4:
                            if (_dbEntries[s].Type != "GoalObj") continue;
                            break;
                        case 5:
                            if (_dbEntries[s].Type != "StartEventObj") continue;
                            break;
                        case 6:
                            if (_dbEntries[s].Type != "Demo") continue;
                            break;
                    }
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(_dbEntries[s].Name != null ? _dbEntries[s].Name : "");
                    ImGui.TableSetColumnIndex(1);
                    if (s == entry.ClassName && setscroll)
                    {
                        ImGui.SetScrollHereY();
                        setscroll = false;
                    }
                    if (ImGui.Selectable(s + $"##{i}", s == entry.ClassName, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        entry = _dbEntries[s];
                        _editClassName = false;
                        _argsel = -1;
                    }
                    i++;
                }
                ImGui.EndTable();
            }
            if (entry.ClassName == null)
                ImGui.BeginDisabled();
            if (entry.ClassName != null)
            {
                if (!ImGui.GetIO().WantTextInput)
                {
                    if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                    {
                        int d = keys.IndexOf(entry.ClassName);
                        entry = _dbEntries.ElementAt(d + 1 < _dbEntries.Count ? d + 1 : d).Value;
                        _editClassName = false;
                        _argsel = -1;
                        setscroll = true;
                    }
                    else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                    {
                        int d = keys.IndexOf(entry.ClassName);
                        entry = _dbEntries.ElementAt(d - 1 > -1 ? d - 1 : d).Value;
                        _editClassName = false;
                        _argsel = -1;
                        setscroll = true;
                    }
                }
            }
            if (ImGui.Button(IconUtils.MINUS, new(ImGui.GetContentRegionAvail().X / 2, default))) // -
            {
                RemoveEntry(entry.ClassName!);
            }
            if (entry.ClassName == null)
                ImGui.EndDisabled();
            ImGui.SameLine(default, ImGui.GetStyle().ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PLUS, new(ImGui.GetContentRegionAvail().X, default))) // +
            {
                for (int i = 0; i < 999; i++)
                {
                    if (_dbEntries.ContainsKey($"NewObj{i}")) continue;
                    entry = new() { ClassName = $"NewObj{i}" };
                    _dbEntries.Add(entry.ClassName, entry);
                    break;
                }
                setscroll = true;
                update = true;
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();

        if (ImGui.BeginChild("RIGHTSIDE", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 30)))
        {
            ImGui.SetWindowFontScale(1.3f);
            if (string.IsNullOrEmpty(entry.ClassName))
                ImGui.TextDisabled("No entry selected");
            else
                ImGui.Text("Class: " + entry.ClassName);
            ImGui.SetWindowFontScale(1f);
            ImGui.Separator();
            ImGui.Spacing();
            if (string.IsNullOrEmpty(entry.ClassName))
                ImGui.BeginDisabled();
            if (!_editClassName)
                ImGui.BeginDisabled();
            ImGui.Text("Class Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Class Name", 20, 30) + style.ItemSpacing.X - 24);
            string className = entry.ClassName ?? "";

            if (ImGui.InputText("##clsname", ref className, 128, ImGuiInputTextFlags.EnterReturnsTrue) && !_dbEntries.ContainsKey(className) && className != entry.ClassName)
            {
                UpdateClassEntry(className != "" ? className : null!);
            }
            if (!_editClassName)
                ImGui.EndDisabled();
            ImGui.SameLine(default, 0);
            if (ImGui.Button(IconUtils.PENCIL))
            {
                _editClassName = !_editClassName;
            }

            ImGui.Text("Entry Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Entry Name", 20, 30) + style.ItemSpacing.X);
            string entryName = entry.Name ?? "";
            ImGui.InputText("##name", ref entryName, 128);
            if (entryName != (entry.Name ?? ""))
            {
                entry.Name = entryName != "" ? entryName : null;
                update = true;
            }

            ImGui.Text("Description:");
            ImGui.SameLine();
            string entryDesc = entry.Description ?? "";
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Description", 20, 30) + style.ItemSpacing.X);
            ImGui.InputText("##Descriptionname", ref entryDesc, 1024);
            if (entryDesc != (entry.Description ?? ""))
            {
                entry.Description = entryDesc != "" ? entryDesc : null!;
                update = true;
            }

            string entryType = entry.Type ?? "Obj";
            if (entryType == "Obj")
            {
                ImGui.Text("Archive:");
                ImGui.SameLine();
                string entryArchive = entry.ArchiveName ?? "";
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Archive", 20, 30) + style.ItemSpacing.X);
                ImGui.InputText("##archive", ref entryArchive, 1024);
                if (entryArchive != (entry.ArchiveName ?? ""))
                {
                    entry.ArchiveName = entryArchive != "" ? entryArchive : null;
                    update = true;
                }
                ImGui.SetItemTooltip("Default model to use for this object if it cannot be found from class or object name");
            }
            else if (entry.ArchiveName != null) // Disable archivename for non objs
            {
                entry.ArchiveName = null;
            }

            ImGui.Text("Needs Rail:");
            ImGui.SameLine();
            bool needsRail = entry.RailRequired;
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Needs Rail", 20, 30) + style.ItemSpacing.X);
            ImGui.Checkbox("##railr", ref needsRail);
            if (needsRail != entry.RailRequired)
            {
                entry.RailRequired = needsRail;
                update = true;
            }

            ImGui.Text("Type:");
            ImGui.SameLine();
            int clsType = Array.IndexOf(actorTypes, entryType);
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Type", 20, 30) + style.ItemSpacing.X);
            ImGui.Combo("##etype", ref clsType, actorTypes, actorTypes.Length);
            if (clsType != Array.IndexOf(actorTypes, entryType))
            {
                entry.Type = actorTypes[clsType] != "Obj" ? actorTypes[clsType] : null; // Since most classes are obj we don't add the property
                update = true;
            }

            Vector2 descriptionSize = new(ImGui.GetContentRegionAvail().X - 2, -3);
            if (ImGui.BeginTabBar("##tabs", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
            {
                if (ImGui.BeginTabItem("Args"))
                {
                    if (
                        ImGui.BeginTable(
                            "ArgTable",
                            3,
                            _tableFlags,
                            new(descriptionSize.X, descriptionSize.Y - 23 - style.ItemSpacing.Y))
                    )
                    {
                        bool isObj = entry.Type == null;
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("Arg", ImGuiTableColumnFlags.None, 0.3f);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
                        ImGui.TableSetupColumn("Name");
                        ImGui.TableHeadersRow();
                        var m = isObj ? 10 : 8;
                        for (int i = 0; i < m; i++)
                        {
                            string arg = $"Arg{i}";
                            string argName = "";
                            string argDescription = "";
                            string argType = "int";
                            if (
                                entry.Args is not null
                                && entry.Args.TryGetValue(arg, out var argData)
                            )
                            {
                                if (argData.Name is not null)
                                    argName = argData.Name;
                                if (argData.Type is not null)
                                    argType = argData.Type;
                                if (argData.Description is not null)
                                    argDescription = argData.Description;
                                if (argData.Default is double)
                                    _args[i] = Convert.ToInt32(argData.Default);
                                else
                                    _args[i] = (int)argData.Default;
                            }
                            else continue;

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            if (ImGui.Selectable(arg, _argsel == i,
                                ImGuiSelectableFlags.SpanAllColumns
                                | ImGuiSelectableFlags.AllowDoubleClick, new(default, 30)))
                            {
                                _argsel = i;
                                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                {
                                    SetArgs(false);
                                    _argEdit = true;
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(argDescription))
                                ImGui.SetItemTooltip(argDescription);
                            ImGui.TableSetColumnIndex(1);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.Text(argType);
                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text(argName);
                        }

                        ImGui.EndTable();

                        if (_argsel < 0 || entry.Args == null)
                            ImGui.BeginDisabled();
                        if (ImGui.Button("Remove ARG", new(ImGui.GetContentRegionAvail().X / 2, default)))
                        {
                            entry.Args.Remove($"Arg{_argsel}");
                            _argsel = -1;
                            update = true;
                        }
                        if (_argsel < 0 || entry.Args == null)
                            ImGui.EndDisabled();
                        ImGui.SameLine(default, style.ItemSpacing.X / 2);
                        if (ImGui.Button("Add ARG", new(ImGui.GetContentRegionAvail().X, default)))
                        {
                            SetArgs();
                            _argEdit = true;
                            update = true;
                        }
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Switches"))
                {
                    if (
                        ImGui.BeginTable(
                            "SwitchTable",
                            3,
                            _tableFlags,
                            new(descriptionSize.X, descriptionSize.Y - 23 - style.ItemSpacing.Y))
                    )
                    {
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("Switch", ImGuiTableColumnFlags.None, 0.3f);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.4f);
                        ImGui.TableSetupColumn("##remove");
                        ImGui.TableHeadersRow();
                        bool swUpdate = false;
                        foreach (string swn in switches)
                        {
                            string swName = $"Switch{swn}";
                            string swDescription = "";
                            string swType = "";

                            if (entry.Switches != null && entry.Switches.ContainsKey(swName) && entry.Switches[swName] != null)
                            {
                                swDescription = entry.Switches[swName]!.Value.Description;
                                swType = entry.Switches[swName]!.Value.Type;
                            }
                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(swn);
                            ImGui.TableSetColumnIndex(1);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            int swi = Array.IndexOf(switchTypes, swType == "" ? "None" : swType);
                            ImGui.Combo($"##swcombo{swn}", ref swi, switchTypes, 3);
                            if (swi != Array.IndexOf(switchTypes, swType == "" ? "None" : swType))
                            {
                                swType = swi == 0 ? "" : switchTypes[swi];
                                swUpdate = true;
                            }
                            ImGui.TableSetColumnIndex(2);
                            if (swi == 0)
                                ImGui.BeginDisabled();
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            if (ImGui.InputText($"##swDesc{swn}", ref swDescription, 128))
                            {
                                swUpdate = true;
                            }
                            if (!string.IsNullOrWhiteSpace(swDescription))
                                ImGui.SetItemTooltip(swDescription);

                            if (swi == 0)
                                ImGui.EndDisabled();

                            if (swUpdate)
                            {
                                if (entry.Switches == null) entry.Switches = new();
                                if (swType == "")
                                    entry.Switches[swName] = null;
                                else
                                    entry.Switches[swName] = new()
                                    {
                                        Description = swDescription,
                                        Type = swType
                                    };
                                swUpdate = false;
                            }
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Description"))
                {
                    ImGui.BeginChild(
                        "##Descriptionbox",
                        descriptionSize,
                        ImGuiChildFlags.Border
                    );
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (entry.Description == null)
                        ImGui.TextDisabled("No Description");
                    else
                        ImGui.TextWrapped(entryDesc);
                    ImGui.EndChild();

                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            if (string.IsNullOrEmpty(entry.ClassName))
                ImGui.EndDisabled();
        }
        if (update && entry.ClassName != null) UpdateEntry();
        ImGui.EndChild();
        if (ImGui.Button("Cancel"))
        {
            Reset();
            _isOpened = false;
            if (_argEdit)
                ImGui.EndDisabled();
            ImGui.CloseCurrentPopup(); ;
            ImGui.EndPopup();
            return;
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize("Save").X - style.ItemSpacing.X * 2);
        if (ImGui.Button("Save"))
        {
            foreach (string s in _modifiedEntries)
            {
                string pth = Path.Join("Resources", "RedPepper-ClassDataBase", "Data", s + ".yml");
                if (!_dbEntries.ContainsKey(s))
                {
                    File.Delete(pth); // Erase the files that will no longer be there
                }
                else
                {
                    PreSaveSwitchSetup(s);
                    YAMLWrapper.Serialize(pth, _dbEntries[s]);
                }
            }
            ClassDatabaseWrapper.ReloadEntries = true;
            Reset();
            _isOpened = false;
            if (_argEdit)
                ImGui.EndDisabled();
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        if (_argEdit)
            ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void UpdateEntry()
    {
        if (!_modifiedEntries.Contains(entry.ClassName))
            _modifiedEntries.Add(entry.ClassName);
        _dbEntries[entry.ClassName] = entry;
    }

    /// <summary>
    /// change the name of the class while removing the previous one
    /// </summary>
    /// <param name="newName"></param>
    private void UpdateClassEntry(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return;
        if (!_modifiedEntries.Contains(entry.ClassName))
            _modifiedEntries.Add(entry.ClassName);
        if (!_modifiedEntries.Contains(newName))
            _modifiedEntries.Add(newName);
        _dbEntries.Remove(entry.ClassName);
        entry.ClassName = newName;
        _dbEntries[entry.ClassName] = entry;
    }

    private void RemoveEntry(string clsName)
    {
        _modifiedEntries.Add(clsName);
        _dbEntries.Remove(clsName!);
        entry = new();
    }

    private void PreSaveSwitchSetup(string switchName)
    {
        if (_dbEntries[switchName].Switches != null)
        {
            if (_dbEntries[switchName].Switches.Count > 0)
            {
                foreach (string sw in switches)
                {
                    if (!_dbEntries[switchName].Switches.ContainsKey($"Switch{sw}")) continue;
                    if (_dbEntries[switchName].Switches[$"Switch{sw}"] == null)
                        _dbEntries[switchName].Switches.Remove($"Switch{sw}");
                }
            }
            if (_dbEntries[switchName].Switches.Count == 0)
            {
                var e = _dbEntries[switchName];
                e.Switches = null!;
                _dbEntries[switchName] = e;
            }
        }
    }

    private void SaveNewArg()
    {
        string tp = "";
        switch (_editArgType)
        {
            case 0:
                _editArgMin = null;
                _editArgMax = null;
                _editEnumValues.Clear();
                tp = "bool";
                break;
            case 1:
                _editEnumValues.Clear();
                tp = "int";
                break;
            case 2:
                _editArgMin = null;
                _editArgMax = null;
                tp = "enum";
                break;
        }

        var arg = new ClassDatabaseWrapper.Arg()
        {
            Default = _editArgDefault,
            Type = tp,
            Description = _editArgDesc,
            Name = _editArgName,
            Values = _editEnumValues.Count > 0 ? _editEnumValues : null,
            Min = _editArgMin,
            Max = _editArgMax
        };
        var oldargs = entry.Args;
        entry.Args = new();
        if (oldargs != null)
            foreach (string a in oldargs.Keys)
            {
                entry.Args[a] = oldargs[a];
            }
        entry.Args[$"Arg{_editArgId}"] = arg;
    }

    private void SetArgs(bool clean = true)
    {
        if (clean)
        {
            _editArgId = 0;
            _editArgType = 1;
            _editArgDesc = "";
            _editArgName = "";
            _editArgDefault = -1;
            _editArgMin = null;
            _editArgMax = null;
            _editEnumValues.Clear();
            _editArgCanOverwrite = false;
        }
        else
        {
            _editArgId = _argsel;
            var arg = entry.Args[$"Arg{_argsel}"];
            _editArgType = arg.Type == null || arg.Type == "int" ? 1 : arg.Type == "bool" ? 0 : 2;
            _editArgDesc = arg.Description ?? "";
            _editArgName = arg.Name ?? "";
            _editArgDefault = (int)arg.Default;
            _editArgMin = arg.Min;
            _editArgMax = arg.Max;
            _editEnumValues = new();
            if (arg.Values != null)
                _editEnumValues = new(arg.Values);
            _editArgCanOverwrite = true;
        }
        _editEnNm = "";
        _editEnVal = -1;
    }

    private void ArgWindow(ImGuiStylePtr style)
    {
        ImGui.OpenPopup("Edit Arg");
        ImGui.SetNextWindowSize(dimensions / 2.8f, ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new(0.5f, 0.5f));
        if (ImGui.BeginPopupModal("Edit Arg", ref _argEdit, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.Popup | ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_editArgCanOverwrite)
                ImGui.BeginDisabled();
            ImGui.Text("Arg ID:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg ID"));
            ImGui.InputInt("##argid", ref _editArgId, 1);
            _editArgId = int.Clamp(_editArgId, 0, 9);
            if (_editArgCanOverwrite)
                ImGui.EndDisabled();

            ImGui.Text("Arg name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg name"));
            ImGui.InputTextWithHint("##argname", "Name", ref _editArgName, 128);

            ImGui.Text("Description:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Description"));
            ImGui.InputTextWithHint("##argdesc", "Description", ref _editArgDesc, 128);
            ImGui.Text("Type:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Type"));
            ImGui.Combo("##argtype", ref _editArgType, argTypes, 3);

            ImGui.Text("Default:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Default"));

            switch (_editArgType)
            {
                case 0:

                    int df = _editArgDefault == -1 ? 0 : 1;
                    ImGui.Combo("##defboolval", ref df, ["False", "True"], 2);
                    _editArgDefault = df == 0 ? -1 : 0;
                    break;

                case 1:
                    ImGui.InputInt("##defval", ref _editArgDefault, 1);
                    int min = _editArgMin ?? -1;
                    int max = _editArgMax ?? -1;
                    if (_editArgMin is null)
                        ImGui.BeginDisabled();
                    ImGui.Text("Min:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Min") - 28);
                    ImGui.InputInt("##minval", ref min, 1);
                    if (min != _editArgMin && _editArgMin != null) _editArgMin = min;
                    if (_editArgMin is null)
                        ImGui.EndDisabled();

                    ImGui.SameLine(default, style.ItemSpacing.X / 2);
                    if (ImGui.Button((_editArgMin is null ? IconUtils.PLUS : IconUtils.MINUS) + "##remMin"))
                    {
                        _editArgMin = _editArgMin is null ? -1 : null;
                    }

                    if (_editArgMax is null)
                        ImGui.BeginDisabled();
                    ImGui.Text("Max:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Max") - 28);
                    ImGui.InputInt("##maxval", ref max, 1);
                    if (max != _editArgMax && _editArgMax != null) _editArgMax = max;
                    if (_editArgMax is null)
                        ImGui.EndDisabled();

                    ImGui.SameLine(default, style.ItemSpacing.X / 2);
                    if (ImGui.Button((_editArgMax is null ? IconUtils.PLUS : IconUtils.MINUS) + "##remMax"))
                    {
                        _editArgMax = _editArgMax is null ? -1 : null;
                    }

                    break;
                case 2:
                    var dfsel = _editEnumValues.Keys.ToList().IndexOf(_editArgDefault);
                    ImGui.Combo("##defval", ref dfsel, _editEnumValues.Values.ToArray(), _editEnumValues.Keys.Count);
                    if (dfsel != _editEnumValues.Keys.ToList().IndexOf(_editArgDefault))
                    {
                        _editArgDefault = _editEnumValues.Keys.ElementAt(dfsel);
                    }
                    int? removeAt = null;
                    if (ImGui.BeginTable("##enumvalues", 3, _tableFlags,
                                        new(ImGui.GetContentRegionAvail().X, 150)))
                    {
                        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.9f);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                        ImGui.TableSetupColumn("##delete", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableHeadersRow();
                        foreach (int i in _editEnumValues.Keys)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            if (ImGui.Selectable(_editEnumValues[i] + $"##{i}sl", false))
                            {
                                _editEnNm = _editEnumValues[i];
                                _editEnVal = i;
                            }
                            ImGui.TableSetColumnIndex(1);
                            if (ImGui.Selectable(i + $"##{i}slid", false))
                            {
                                _editEnNm = _editEnumValues[i];
                                _editEnVal = i;
                            }
                            ImGui.TableSetColumnIndex(2);
                            if (ImGui.Selectable(IconUtils.MINUS + $"##{i}dl"))
                            {
                                removeAt = i;
                            }
                        }
                        ImGui.EndTable();
                    }
                    if (removeAt != null)
                    {
                        _editEnumValues.Remove((int)removeAt);
                    }
                    if (_editEnumValues.ContainsKey(_editEnVal))
                        ImGui.BeginDisabled();
                    if (ImGui.Button("Add", new(ImGui.GetContentRegionAvail().X, 25)))
                    {
                        _editEnumValues[_editEnVal] = _editEnNm;
                    }
                    if (_editEnumValues.ContainsKey(_editEnVal))
                        ImGui.EndDisabled();

                    ImGui.Text("Name:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg Name"));
                    if (ImGui.InputText("##opName", ref _editEnNm, 128) && _editEnumValues.ContainsKey(_editEnVal))
                    {
                        _editEnumValues[_editEnVal] = _editEnNm;
                    }
                    if (!string.IsNullOrWhiteSpace(_editEnNm))
                        ImGui.SetItemTooltip(_editEnNm);
                    ImGui.Text("Value:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg Value"));
                    ImGui.InputInt("##opVal", ref _editEnVal, 1);
                    break;
            }

            ImGui.Separator();
            bool canSave = entry.Args == null || !entry.Args.ContainsKey($"Arg{_editArgId}") || _editArgCanOverwrite;

            if (ImGui.Button("Cancel", new(ImGui.GetContentRegionAvail().X / 2, 30)))
            {
                _argEdit = false;
            }
            ImGui.SameLine(default, style.ItemSpacing.X / 2);

            if (!canSave)
                ImGui.BeginDisabled();
            if (ImGui.Button("OK", new(ImGui.GetContentRegionAvail().X, 30)))
            {
                SaveNewArg();
                UpdateEntry();
                _argEdit = false;
            }
            if (!canSave)
                ImGui.EndDisabled();

        }
        ImGui.End();
    }


}
