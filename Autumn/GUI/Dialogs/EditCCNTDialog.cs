using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering.Storage;
using Autumn.FileSystems;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using ImGuiNET;
using Autumn.Wrappers;
using Autumn.Utils;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that allows the user to change the classes of objects.
/// </summary>
internal class EditCreatorClassNameTable(MainWindowContext _window)
{

    private bool _isOpened = false;
    private Dictionary<string, string> _tmpCCNT = new();
    private string _search = "";
    private string _oname = "";
    private string _name = "";
    private string _oclass = "";
    private string _class = "";

    Vector2 dimensions = new(742, 520);
    ImGuiWidgets.InputComboBox classCombo = new();

    public void Open()
    {
        Reset();
        _tmpCCNT = new(_window.ContextHandler.FSHandler.ReadCreatorClassNameTable());
        _isOpened = true;
    }


    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        dimensions = new Vector2(742, 520) * _window.ScalingFactor;
        _tmpCCNT = new();
        _isOpened = false;
        _search = "";
        _oname = "";
        _name = "";
        _oclass = "";
        _class = "";
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && !ImGui.GetIO().WantTextInput) // prevent exiting when input is focused
        {
            Reset();
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("CreatorClassNameTable Editor");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new(0.5f, 0.5f));

        if (
            !ImGui.BeginPopupModal(
                "CreatorClassNameTable Editor",
                ref _isOpened,
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar
            )
        )
            return;

        dimensions = ImGui.GetWindowSize();
        var style = ImGui.GetStyle();
        if (ImGui.BeginChild("LEFTSIDE", new(dimensions.X * 18 / 30, dimensions.Y)))
        {
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            ImGui.InputTextWithHint("##SEARCHBOX", "Class or Object name", ref _search, 100);
            if (ImGui.BeginTable("ClassObjectTable", 2,
                                                    ImGuiTableFlags.RowBg
                                                    | ImGuiTableFlags.BordersOuter
                                                    | ImGuiTableFlags.BordersV
                                                    | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - 1, ImGui.GetWindowHeight() - 95)))
            {
                int i = 0;
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableHeadersRow();
                foreach (string s in _tmpCCNT.Keys)
                {
                    if (!string.IsNullOrEmpty(_search)
                    && !s.Contains(_search, StringComparison.InvariantCultureIgnoreCase)
                    && !_tmpCCNT[s].Contains(_search, StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (ImGui.Selectable(s + $"##{i}", s == _oname, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _oname = s;
                        _name = s;
                        _oclass = _tmpCCNT[s];
                        _class = _tmpCCNT[s];
                    }
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(_tmpCCNT[s]);
                    i++;
                }
                ImGui.EndTable();
            }
            if (ImGui.Button(IconUtils.MINUS, new(ImGui.GetWindowWidth() / 2, default))) // -
            {
                _tmpCCNT.Remove(_oname);
                _oname = "";
                _oclass = "";
                _name = "";
                _class = "";
            }
            ImGui.SameLine(default, ImGui.GetStyle().ItemSpacing.X / 2);
            if (ImGui.Button(IconUtils.PLUS, new(ImGui.GetWindowWidth() / 2, default))) // +
            {
                for (int i = 0; i < 999; i++)
                {
                    if (_tmpCCNT.ContainsKey($"NewObj{i}")) continue;
                    _oname = $"NewObj{i}";
                    _oclass = "FixMapParts"; // Probably what most people will want to add by default
                    _tmpCCNT.Add(_oname, _oclass);
                    _name = _oname;
                    _class = _oclass;
                    break;
                }
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();

        ClassDatabaseWrapper.DatabaseEntry? dbEntry = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(_oclass) ? ClassDatabaseWrapper.DatabaseEntries[_oclass] : null;

        if (ImGui.BeginChild("RIGHTSIDE", new(dimensions.X * 12 / 30, dimensions.Y)))
        {
            ImGui.SetWindowFontScale(1.3f);
            if (string.IsNullOrEmpty(_name))
                ImGui.TextDisabled("No object selected");
            else
                ImGui.Text(_name);
            ImGui.SetWindowFontScale(1f);
            ImGui.Separator();
            ImGui.Spacing();
            if (_oname == "")
                ImGui.BeginDisabled();
            ImGui.Text("Object Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Object Name", 20, 30) - 20);
            ImGui.InputText("##name", ref _name, 128);
            if (_oname != _name && _name != "" && !_tmpCCNT.ContainsKey(_name))
            {
                _tmpCCNT.Remove(_oname);
                _tmpCCNT.Add(_name, _class);
                _oname = _name;
            }

            ImGui.Text("Class:");
            var beginChild = ImGui.GetCursorPos();
            ImGui.SameLine();
            var beginClass = ImGui.GetCursorPos();

            ImGui.SetCursorPos(beginChild);
            ImGui.SetWindowFontScale(1.3f);
            if (dbEntry == null || dbEntry.Value.Name == null)
                ImGui.TextDisabled("Undocumented class");
            else
                ImGui.Text(dbEntry.Value.Name);
            ImGui.SetWindowFontScale(1f);

            ImGui.BeginChild(
                "##Description",
                new Vector2(ImGui.GetWindowWidth() - style.ItemSpacing.X * 3, -95),
                ImGuiChildFlags.Border
            );
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (dbEntry == null || dbEntry.Value.Description == null)
                ImGui.TextDisabled("No Description");
            else
                ImGui.TextWrapped(dbEntry.Value.Description);
            ImGui.EndChild();
            var beginLast = ImGui.GetCursorPos();
            ImGui.SetCursorPos(beginClass);

            List<string> comboStrings = _tmpCCNT.Values.Distinct().Where(x=> x != _class).ToList();
            comboStrings = ClassDatabaseWrapper.DatabaseEntries.Keys.ToList();
            classCombo.Use("ClassComboString", ref _class, comboStrings, ImGuiWidgets.SetPropertyWidthGen("Class", 20, 30) - 20);
            
            if (_oname == "")
                ImGui.EndDisabled();
            ImGui.SetCursorPos(beginLast);
            if (_oclass != _class && !string.IsNullOrWhiteSpace(_class))
            {
                _tmpCCNT[_name] = _class;
                _oclass = _class;
            }



            if (ImGui.Button("Merge Objects from another database", new(ImGui.GetWindowWidth() - style.ItemSpacing.X * 2 - 8, default)))
            {

                SingleFileChooserContext fileChooserContext = new(_window.ContextHandler, _window.WindowManager);
                _window.WindowManager.Add(fileChooserContext);

                fileChooserContext.SuccessCallback += result =>
                {
                    var r = _window.ContextHandler.FSHandler.TryReadCCNT(result[0]);
                    if (r != null)
                    {
                        if (r.Keys.ToList() != _tmpCCNT.Keys.ToList())
                        {
                            foreach (string s in r.Keys)
                            {
                                if (_tmpCCNT.ContainsKey(s)) continue;
                                else _tmpCCNT.Add(s, r[s]);
                            }
                        }
                    }
                };
            }
            if (ImGui.Button("Cancel", new(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X * 2, default)))
            {
                Reset();
                _isOpened = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine(default, ImGui.GetStyle().ItemSpacing.X / 2);
            if (ImGui.Button("Save", new(ImGui.GetWindowWidth() / 2 - style.ItemSpacing.X * 2 + 4, default)))
            {
                _window.ContextHandler.FSHandler.WriteCreatorClassNameTable(_tmpCCNT);
                Reset();

                _isOpened = false;
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndChild();

        ImGui.EndPopup();
    }
}
