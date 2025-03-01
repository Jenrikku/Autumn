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
    private int[] _args = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
    private int _argsel = -1;
    string[] switches = ["A", "B", "Appear", "Kill", "DeadOn"];
    string[] switchStates = ["None", "Read", "Write"];
    private string _swsel = "";
    private SortedDictionary<string, ClassDatabaseWrapper.DatabaseEntry> _dbEntries;
    private List<string> _modifiedEntries = new();
    private string _search = "";
    private ClassDatabaseWrapper.DatabaseEntry oldEntry;
    private ClassDatabaseWrapper.DatabaseEntry entry;
    private string newClassName = "";
    Vector2 dimensions = new(742, 520);
    bool newPopup = false;
    string[] ActorTypes = ["Obj", "AreaObj", "CameraAreaObj", "GoalObj", "StartEventObj", "Demo"];
    string[] filter = ["All"];
    int filterIdx = 0;
    bool editClassName = false;
    bool _isEditor = true;


    bool debugflag = false;


    bool editArgCanOverwrite = false;
    int editArgId = 0; // 0 through 9
    int editArgType = 1; // Bool, Int, Enum
    string[] editArgTypes = ["bool", "int", "enum"];
    string editArgDesc = "";
    string editArgName = "";
    int editArgDefault = -1;
    int? editArgMin;
    int? editArgMax;
    Dictionary<int, string> editEnumValues = new();
    string curEnumName = "";
    int curEnumVal = -1;

    public void Open()
    {
        Reset();
        _isOpened = true;
        _dbEntries = new(ClassDatabaseWrapper.DatabaseEntries);
        filter = filter.Concat(ActorTypes).ToArray();
        _isEditor = _window.ContextHandler.SystemSettings.EnableDBEditor;
    }
    public void Reset()
    {
        dimensions = new Vector2(742, 520) * _window.ScalingFactor;
        oldEntry = new();
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
            if (newPopup)
                newPopup = false;
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

        if (newPopup)
        {
            ImGui.OpenPopup("Edit Arg");
            ImGui.SetNextWindowSize(dimensions / 2.8f, ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Edit Arg", ref newPopup, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.Popup | ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (editArgCanOverwrite)
                    ImGui.BeginDisabled();
                ImGui.Text("Arg ID:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg ID"));
                ImGui.InputInt("##argid", ref editArgId, 1);
                editArgId = int.Clamp(editArgId, 0, 9);
                if (editArgCanOverwrite)
                    ImGui.EndDisabled();
                ImGui.Text("Arg name:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg name"));
                ImGui.InputTextWithHint("##argname", "Name", ref editArgName, 128);
                ImGui.Text("Description:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Description"));
                ImGui.InputTextWithHint("##argdesc", "Description", ref editArgDesc, 128);
                ImGui.Text("Type:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Type"));
                ImGui.Combo("##argtype", ref editArgType, editArgTypes, 3);
                ImGui.Text("Default:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Default"));

                switch (editArgType)
                {
                    case 0:

                        int df = editArgDefault == -1 ? 0 : 1;
                        ImGui.Combo("##defboolval", ref df, ["False", "True"], 2);
                        editArgDefault = df == 0 ? -1 : 0;
                        break;

                    case 1:
                        ImGui.InputInt("##defval", ref editArgDefault, 1);
                        int min = editArgMin ?? -1;
                        int max = editArgMax ?? -1;
                        if (editArgMin is null)
                            ImGui.BeginDisabled();
                        ImGui.Text("Min:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Min") - 28);
                        ImGui.InputInt("##minval", ref min, 1);
                        if (min != editArgMin && editArgMin != null) editArgMin = min;
                        if (editArgMin is null)
                            ImGui.EndDisabled();

                        ImGui.SameLine(default, style.ItemSpacing.X/2);
                        if (ImGui.Button((editArgMin is null ? IconUtils.PLUS : IconUtils.MINUS) + "##remMin"))
                        {
                            editArgMin = editArgMin is null ? -1 : null;
                        }

                        if (editArgMax is null)
                            ImGui.BeginDisabled();
                        ImGui.Text("Max:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Max")- 28);
                        ImGui.InputInt("##maxval", ref max, 1);
                        if (max != editArgMax && editArgMax != null) editArgMax = max;
                        if (editArgMax is null)
                            ImGui.EndDisabled();

                        ImGui.SameLine(default, style.ItemSpacing.X/2);
                        if (ImGui.Button((editArgMax is null ? IconUtils.PLUS : IconUtils.MINUS) + "##remMax"))
                        {
                            editArgMax = editArgMax is null ? -1 : null;  
                        }

                        break;
                    case 2:
                        var dfsel = editEnumValues.Keys.ToList().IndexOf(editArgDefault);
                        ImGui.Combo("##defval", ref dfsel, editEnumValues.Values.ToArray(), editEnumValues.Keys.Count);
                        if (dfsel != editEnumValues.Keys.ToList().IndexOf(editArgDefault))
                        {
                            editArgDefault = editEnumValues.Keys.ElementAt(dfsel);
                        }
                        int removeAt = -1;
                        if (ImGui.BeginTable("##enumvalues", 3, ImGuiTableFlags.RowBg
                                                    | ImGuiTableFlags.BordersOuter
                                                    | ImGuiTableFlags.BordersV
                                                    | ImGuiTableFlags.ScrollY
                                                    | ImGuiTableFlags.Resizable,
                                                    new(ImGui.GetContentRegionAvail().X, 150)
                                                    ))
                        {
                            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.9f);
                            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                            ImGui.TableSetupColumn("##delete", ImGuiTableColumnFlags.None, 0.1f);
                            ImGui.TableHeadersRow();
                            foreach (int i in editEnumValues.Keys)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableSetColumnIndex(0);
                                if (ImGui.Selectable(editEnumValues[i] + $"##{i}sl", false))
                                {
                                    curEnumName = editEnumValues[i];
                                    curEnumVal = i;
                                }
                                ImGui.TableSetColumnIndex(1);
                                if (ImGui.Selectable(i + $"##{i}slid", false))
                                {
                                    curEnumName = editEnumValues[i];
                                    curEnumVal = i;
                                }
                                ImGui.TableSetColumnIndex(2);
                                if (ImGui.Selectable(IconUtils.MINUS + $"##{i}dl"))
                                {
                                    removeAt = i;
                                }
                            }
                            ImGui.EndTable();
                        }
                        if (removeAt > -1)
                        {
                            editEnumValues.Remove(removeAt);
                        }
                        if (editEnumValues.ContainsKey(curEnumVal))
                            ImGui.BeginDisabled();
                        if (ImGui.Button("Add", new(ImGui.GetContentRegionAvail().X, 25)))
                        {
                            editEnumValues[curEnumVal] = curEnumName;
                        }
                        if (editEnumValues.ContainsKey(curEnumVal))
                            ImGui.EndDisabled();

                        ImGui.Text("Name:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg Name"));
                        if (ImGui.InputText("##opName", ref curEnumName, 128) && editEnumValues.ContainsKey(curEnumVal))
                        {
                            editEnumValues[curEnumVal] = curEnumName;
                        }
                        if (!string.IsNullOrWhiteSpace(curEnumName))
                            ImGui.SetItemTooltip(curEnumName);
                        ImGui.Text("Value:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Arg Value"));
                        ImGui.InputInt("##opVal", ref curEnumVal, 1);
                        break;
                }


                ImGui.Separator();
                bool canSave = entry.Args == null || !entry.Args.ContainsKey($"Arg{editArgId}") || editArgCanOverwrite;

                if (ImGui.Button("Cancel", new(ImGui.GetContentRegionAvail().X / 2, 30)))
                {
                    newPopup = false;
                }
                ImGui.SameLine(default, style.ItemSpacing.X / 2);

                if (!canSave)
                    ImGui.BeginDisabled();
                if (ImGui.Button("OK", new(ImGui.GetContentRegionAvail().X, 30)))
                {
                    SaveNewArg();
                    newPopup = false;
                }
                if (!canSave)
                    ImGui.EndDisabled();

            }
            ImGui.End();
        }
        if (newPopup)
            ImGui.BeginDisabled();
        if (ImGui.BeginChild("LEFTSIDE", new(ImGui.GetContentRegionAvail().X * 18 / 30, ImGui.GetContentRegionAvail().Y - 30)))
        {
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X / 2);
            ImGui.InputTextWithHint("##SEARCHBOX", "Class or Documented name", ref _search, 100);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X / 2);
            ImGui.Combo("##Filter", ref filterIdx, filter, filter.Length);
            if (ImGui.BeginTable("ClassTable", 2,
                                                    ImGuiTableFlags.RowBg
                                                    | ImGuiTableFlags.BordersOuter
                                                    | ImGuiTableFlags.BordersV
                                                    | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - 1, ImGui.GetContentRegionAvail().Y - 30)))
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
                    switch (filterIdx)
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
                    if (ImGui.Selectable(s + $"##{i}", s == oldEntry.ClassName, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        oldEntry = _dbEntries[s];
                        entry = _dbEntries[s];
                        editClassName = false;
                        _argsel = -1;
                    }
                    i++;
                }
                ImGui.EndTable();
            }
            if (oldEntry.ClassName == null)
                ImGui.BeginDisabled();
            if (ImGui.Button(IconUtils.MINUS, new(ImGui.GetContentRegionAvail().X / 2, default))) // -
            {
                _dbEntries.Remove(oldEntry.ClassName);
                oldEntry = new();
                entry = new();
            }
            if (oldEntry.ClassName == null)
                ImGui.EndDisabled();
            ImGui.SameLine(default, ImGui.GetStyle().ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PLUS, new(ImGui.GetContentRegionAvail().X, default))) // +
            {
                //newPopup = true;

                for (int i = 0; i < 999; i++)
                {
                    if (_dbEntries.ContainsKey($"NewObj{i}")) continue;
                    oldEntry = new() { ClassName = $"NewObj{i}" };
                    entry = oldEntry;
                    _dbEntries.Add(oldEntry.ClassName, oldEntry);
                    break;
                }
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();

        bool update = false;
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
            if (!editClassName)
                ImGui.BeginDisabled();
            ImGui.Text("Class Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Class Name", 20, 30) + style.ItemSpacing.X - 24);
            string className = entry.ClassName ?? "";

            if (ImGui.InputText("##clsname", ref className, 128, ImGuiInputTextFlags.EnterReturnsTrue) && !_dbEntries.ContainsKey(className) && className != entry.ClassName)
            {
                UpdateClassEntry(className != "" ? className : null);
            }
            if (!editClassName)
                ImGui.EndDisabled();
            ImGui.SameLine(default, 0);
            if (ImGui.Button(IconUtils.PENCIL))
            {
                editClassName = !editClassName;
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
                entry.Description = entryDesc != "" ? entryDesc : null;
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
            bool RailRequired = entry.RailRequired;
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Needs Rail", 20, 30) + style.ItemSpacing.X);
            ImGui.Checkbox("##railr", ref RailRequired);
            if (RailRequired != entry.RailRequired)
            {
                entry.RailRequired = RailRequired;
                update = true;
            }

            ImGui.Text("Type:");
            ImGui.SameLine();
            int itype = Array.IndexOf(ActorTypes, entryType);
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Type", 20, 30) + style.ItemSpacing.X);
            ImGui.Combo("##etype", ref itype, ActorTypes, ActorTypes.Length);
            if (itype != Array.IndexOf(ActorTypes, entryType))
            {
                entry.Type = ActorTypes[itype] != "Obj" ? ActorTypes[itype] : null; // Since most classes are obj we don't add the property
                update = true;
            }

            // ImGui.SetWindowFontScale(1.3f);
            // if (entry.Name == null)
            //     ImGui.TextDisabled("Undocumented class");
            // else
            //     ImGui.Text(entry.Name);
            // ImGui.SetWindowFontScale(1f);

            Vector2 descriptionSize = new(ImGui.GetContentRegionAvail().X - 2, -3);
            if (ImGui.BeginTabBar("##tabs", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
            {
                if (ImGui.BeginTabItem("Args"))
                {
                    if (
                        ImGui.BeginTable(
                            "ArgTable",
                            3,
                            ImGuiTableFlags.ScrollY
                            | ImGuiTableFlags.RowBg
                            | ImGuiTableFlags.BordersOuter
                            | ImGuiTableFlags.BordersV
                            | ImGuiTableFlags.Resizable,
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
                            if (ImGui.Selectable(arg, _argsel == i, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick, new(default, 30)))
                            {
                                _argsel = i;
                                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                {
                                    SetArgs(false);
                                    newPopup = true;
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
                            newPopup = true;
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
                            ImGuiTableFlags.ScrollY
                            | ImGuiTableFlags.RowBg
                            | ImGuiTableFlags.BordersOuter
                            | ImGuiTableFlags.BordersV
                            | ImGuiTableFlags.Resizable,
                            new(descriptionSize.X, descriptionSize.Y - 23 - style.ItemSpacing.Y))
                    )
                    {
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableSetupColumn("Switch", ImGuiTableColumnFlags.None, 0.3f);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.4f);
                        ImGui.TableSetupColumn("##remove");
                        ImGui.TableHeadersRow();
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
                            int swi = Array.IndexOf(switchStates, swType == "" ? "None" : swType);
                            ImGui.Combo($"##swcombo{swn}", ref swi, switchStates, 3);
                            if (swi != Array.IndexOf(switchStates, swType == "" ? "None" : swType))
                            {
                                swType = swi == 0 ? "" : switchStates[swi];
                                update = true;
                            }
                            ImGui.TableSetColumnIndex(2);
                            if (swi == 0)
                                ImGui.BeginDisabled();
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            if (ImGui.InputText($"##swDesc{swn}", ref swDescription, 128))
                            {
                                update = true;
                            }
                            if (!string.IsNullOrWhiteSpace(swDescription))
                                ImGui.SetItemTooltip(swDescription);

                            if (swi == 0)
                                ImGui.EndDisabled();

                            if (update)
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
                                update = false;
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
            var beginLast = ImGui.GetCursorPos();
            //ImGui.SetCursorPos(beginClass);

            if (string.IsNullOrEmpty(entry.ClassName))
                ImGui.EndDisabled();

            //ImGui.SetCursorPos(beginLast);
        }
        if (update && entry.ClassName != null) UpdateEntry();
        ImGui.EndChild();
        if (ImGui.Button("Cancel"))
        {
            Reset();
            _isOpened = false;
            if (newPopup)
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
                if (!_dbEntries.ContainsKey(s)) // Erase the files that will no longer be there
                {
                    // File.Delete(Path.Join("Resources", "RedPepper-ClassDataBase", "Data", s + ".yml"));
                }
                else
                {
                    if (_dbEntries[s].Switches != null)
                    {
                        if (_dbEntries[s].Switches.Count > 0)
                        {
                            foreach (string sw in switches)
                            {
                                if (!_dbEntries[s].Switches.ContainsKey($"Switch{sw}")) continue;
                                if (_dbEntries[s].Switches[$"Switch{sw}"] == null)
                                    _dbEntries[s].Switches.Remove($"Switch{sw}");
                            }
                        }
                        if (_dbEntries[s].Switches.Count == 0)
                        {
                            var e = _dbEntries[s];
                            e.Switches = null;
                            _dbEntries[s] = e;
                        }
                    }
                    YAMLWrapper.Serialize(Path.Join("Resources", "RedPepper-ClassDataBase", "Data", s + ".yml"), _dbEntries[s]);
                }
            }
            ClassDatabaseWrapper.ReloadEntries = true;
            Reset();

            _isOpened = false;
            if (newPopup)
                ImGui.EndDisabled();
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        if (newPopup)
            ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void UpdateEntry()
    {
        if (!_modifiedEntries.Contains(entry.ClassName))
            _modifiedEntries.Add(entry.ClassName);
        oldEntry = entry;
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
        oldEntry = entry;
        _dbEntries.Remove(entry.ClassName);
        entry.ClassName = newName;
        _dbEntries[entry.ClassName] = entry;
    }

    private void SaveNewArg()
    {
        string tp = "";
        switch (editArgType)
        {
            case 0:
                editArgMin = null;
                editArgMax = null;
                editEnumValues.Clear();
                tp = "bool";
                break;
            case 1:
                editEnumValues.Clear();
                tp = "int";
                break;
            case 2:
                editArgMin = null;
                editArgMax = null;
                tp = "enum";
                break;
        }
        if (entry.Args == null)
        {
            entry.Args = new();
        }

        var arg = new ClassDatabaseWrapper.Arg()
        {
            Default = editArgDefault,
            Type = tp,
            Description = editArgDesc,
            Name = editArgName,
            Values = editEnumValues.Count > 0 ? editEnumValues : null,
            Min = editArgMin,
            Max = editArgMax
        };
        entry.Args[$"Arg{editArgId}"] = arg;
        UpdateEntry();
    }

    private void SetArgs(bool clean = true)
    {
        if (clean)
        {
            editArgId = 0;
            editArgType = 1;
            editArgDesc = "";
            editArgName = "";
            editArgDefault = -1;
            editArgMin = null;
            editArgMax = null;
            editEnumValues.Clear();
            editArgCanOverwrite = false;
        }
        else
        {
            editArgId = _argsel;
            var arg = entry.Args[$"Arg{_argsel}"];
            editArgType = arg.Type == null || arg.Type == "int" ? 1 : arg.Type == "bool" ? 0 : 2;
            editArgDesc = arg.Description ?? "";
            editArgName = arg.Name ?? "";
            editArgDefault = (int)arg.Default;
            editArgMin = arg.Min;
            editArgMax = arg.Max;
            editEnumValues = new();
            if (arg.Values != null)
                editEnumValues = new(arg.Values);
            editArgCanOverwrite = true;
        }
        curEnumName = "";
        curEnumVal = -1;
    }
}
