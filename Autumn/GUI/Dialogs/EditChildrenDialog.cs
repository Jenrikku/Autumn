using System.Numerics;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that allows the user to set, remove and move objects as children of another.
/// </summary>
internal class EditChildrenDialog(MainWindowContext _window)
{

    private bool _isOpened = false;
    private string _name = string.Empty;
    private List<StageObj>[] _selectedObjs = [new(), new()]; // General / Children
    private List<StageObj> _newChildren = new();
    private StageObj? _parent;
    private List<StageObj> _oldChildren = new();
    private IEnumerable<IStageSceneObj>? _sceneObjs;
    private string _search = "";

    Vector2 dimensions = new(800, 450);


    public void Open(StageObj stageObj)
    {
        Reset();
        _parent = stageObj;

        if (_parent.Children is not null)
        {
            _oldChildren = _parent.Children;
            _newChildren = new(_parent.Children);
        }

        _sceneObjs = _window.CurrentScene!.EnumerateStageSceneObjs();
        _name = _parent.Name;
        _isOpened = true;
    }

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        dimensions = new Vector2(800, 450) * _window.ScalingFactor;
        _name = string.Empty;
        _parent = null;
        _oldChildren = new();
        _newChildren = new();
        _selectedObjs[0].Clear();
        _selectedObjs[1].Clear();
        _isOpened = false;
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Reset();
            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.OpenPopup("Modify " + _name + "'s children.");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new(0.5f, 0.5f));

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Modify " + _name + "'s children.",
                ref _isOpened
            )
        )
            return;

        dimensions = ImGui.GetWindowSize();

        ImGui.InputText("SEARCHBOX", ref _search, 100);
        Vector2 tableDimensions = new(dimensions.X / 2 - 27, dimensions.Y - 96);
        float tablestart = ImGui.GetCursorPosY();

        if (ImGui.BeginChild("LEFT", tableDimensions))
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("Scene objects:");
            ImGui.SetWindowFontScale(1.0f);

            tablestart += ImGui.GetCursorPosY();
            if (ImGui.BeginTable("ObjectTable", 2,
                                                ImGuiTableFlags.RowBg
                                                | ImGuiTableFlags.Resizable
                                                | ImGuiTableFlags.BordersOuter
                                                | ImGuiTableFlags.BordersV
                                                | ImGuiTableFlags.ScrollY, new(tableDimensions.X - 3, tableDimensions.Y - 30)))
            {
                int i = 0;
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.None, 0.60f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.40f);
                ImGui.TableHeadersRow();

                foreach (IStageSceneObj obj in _sceneObjs)
                {
                    StageObj stageObj = obj.StageObj;

                    if (_search != "" && !stageObj.Name.Contains(_search, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (!(!_newChildren.Contains(stageObj) && _oldChildren.Contains(stageObj)))
                    {
                        if (stageObj == _parent.Parent || _newChildren.Contains(stageObj) || (stageObj.Type != StageObjType.Regular && stageObj.Type != StageObjType.Area) || (stageObj.Type == StageObjType.Area && stageObj.Name != "ObjectChildArea") || stageObj.FileType != _parent.FileType || stageObj == _parent)
                            continue;
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushID("ObjectSelect" + i);

                    bool contained = _selectedObjs[0].Contains(stageObj);

                    if (ImGui.Selectable(stageObj.Name, contained, ImGuiSelectableFlags.DontClosePopups | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
                    {
                        if (contained)
                            _selectedObjs[0].Remove(stageObj);
                        else
                            _selectedObjs[0].Add(stageObj);

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            AxisAlignedBoundingBox aabb = obj.AABB * stageObj.Scale;
                            _window.CurrentScene!.Camera.LookFrom(
                                stageObj.Translation * 0.01f,
                                aabb.GetDiagonal() * 0.01f
                            );
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.SetTooltip("General object: " + stageObj.Name);
                        ImGui.EndTooltip();
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(stageObj.Type.ToString());
                    i++;
                }

                ImGui.EndTable();
            }

        }

        ImGui.EndChild();
        //ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xff0000ff);
        ImGui.SameLine(default, 5f);

        ImGui.SetCursorPosY(tablestart);
        if (ImGui.BeginChild("MIDDLE", new(24, tableDimensions.Y - 24)))
        {
            if (ImGuiWidgets.ArrowButton("r", ImGuiDir.Right, new(default, ImGui.GetWindowHeight()/2 - ImGui.GetStyle().ItemSpacing.Y)))
            {
                MoveToChildren();
            }

            if (ImGuiWidgets.ArrowButton("l", ImGuiDir.Left,  new(default, ImGui.GetWindowHeight()/2 - ImGui.GetStyle().ItemSpacing.Y)))
            {
                MoveToGeneral();
            }
        }
        ImGui.EndChild();
        //ImGui.PopStyleColor();
        ImGui.SameLine();

        List<int> move = new(); // Id, Up(true) or Down(false)
        bool moveb = false; // Id, Up(true) or Down(false)
        if (ImGui.BeginChild("RIGHT", tableDimensions))
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("Children:");
            ImGui.SetWindowFontScale(1.0f);

            if (ImGui.BeginTable("ChildrenTable", 2,
                                                    ImGuiTableFlags.RowBg
                                                    | ImGuiTableFlags.Resizable
                                                | ImGuiTableFlags.BordersOuter
                                                | ImGuiTableFlags.BordersV
                                                | ImGuiTableFlags.ScrollY, new(tableDimensions.X - 2, tableDimensions.Y - 60)))
            {
                int i = 0;
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.None, 0.60f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.40f);
                ImGui.TableHeadersRow();

                foreach (StageObj stageObj in _newChildren)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushID("ChildSelect" + i);

                    bool contained = _selectedObjs[1].Contains(stageObj);

                    if (ImGui.Selectable(stageObj.Name, contained, ImGuiSelectableFlags.DontClosePopups | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
                    {
                        if (contained)
                            _selectedObjs[1].Remove(stageObj);
                        else
                            _selectedObjs[1].Add(stageObj);

                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            AxisAlignedBoundingBox aabb = new(2);
                            _window.CurrentScene!.Camera.LookFrom(
                                stageObj.Translation * 0.01f,
                                aabb.GetDiagonal() * 0.01f
                            );
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.SetTooltip("Child: " + stageObj.Name);
                        ImGui.EndTooltip();

                    }
                    if (contained)
                    {
                        if (_newChildren.IndexOf(stageObj) != 0)
                        {
                            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                            {
                                move.Add(_newChildren.IndexOf(stageObj));
                                moveb = true;
                            }
                        }
                        if (_newChildren.IndexOf(stageObj) != _newChildren.Count - 1)
                        {
                            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                            {
                                move.Add(_newChildren.IndexOf(stageObj));
                                moveb = false;
                            }
                        }
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(stageObj.Type.ToString());
                    i++;
                }

                ImGui.EndTable();
            }

            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() / 2);
            if (ImGuiWidgets.ArrowButton("upbt", ImGuiDir.Up, new(ImGui.GetWindowWidth() / 2, -1)))
            {
                foreach (StageObj s in _selectedObjs[1])
                {
                    if (_newChildren.IndexOf(s) != 0)
                        move.Add(_newChildren.IndexOf(s));
                }
                move.Sort();
                moveb = true;
            }
            ImGui.SameLine();
            if (ImGuiWidgets.ArrowButton("dwbt", ImGuiDir.Down, new(ImGui.GetWindowWidth() / 2, -1)))
            {
                foreach (StageObj s in _selectedObjs[1])
                {
                    if (_newChildren.IndexOf(s) != _newChildren.Count - 1)
                        move.Add(_newChildren.IndexOf(s));
                }
                move.Sort();
                moveb = false;
            }

            if (move.Count > 0)
            {
                if (moveb)
                    for (int ii = 0; ii < move.Count; ii++)
                    {
                        var ch = _newChildren[move[ii]];
                        _newChildren.RemoveAt(move[ii]);
                        _newChildren.Insert(move[ii] - 1, ch);
                    }
                else
                    for (int ii = move.Count - 1; ii > -1; ii--)
                    {
                        var ch = _newChildren[move[ii]];
                        _newChildren.RemoveAt(move[ii]);
                        _newChildren.Insert(move[ii] + 1, ch);
                    }
            }
        }
        ImGui.EndChild();

        //ImGui.TextColored(new Vector4(1, 0, 0, 1), dimensions.X+", "+ dimensions.Y);
        //ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - 90);
        ImGui.SetCursorPosY(dimensions.Y - ImGui.GetTextLineHeight() - 14);

        if (ImGui.Button("Ok", new(80, 0)))
        {
            // finish setting up then reset the dialog
            if (_parent is not null && _parent.Children is null)
            {
                _parent.Children = new();
            }
            else
            {

                foreach (StageObj oldChild in _parent!.Children!)
                {
                    if (!_newChildren.Contains(oldChild))
                    {
                        _window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).UnlinkChild(oldChild);
                    }
                }

                _parent.Children.Clear();
            }

            for (int i = 0; i < _newChildren.Count(); i++)
            {
                _window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).SetChild(_newChildren[i], _parent);
	    }

            Reset();

            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void MoveToChildren() // Gen -> Children
    {
        foreach (StageObj so in _selectedObjs[0])
            _newChildren.Add(so);

        _selectedObjs[0].Clear();
    }

    private void MoveToGeneral() // Gen <- Children
    {
        foreach (StageObj so in _selectedObjs[1])
            _newChildren.Remove(so);

        _selectedObjs[1].Clear();
    }
}
