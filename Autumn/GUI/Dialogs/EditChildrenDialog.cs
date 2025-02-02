using System.Numerics;
using Autumn.FileSystems;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that allows the user to set and unset objects as children of another.
/// </summary>
internal class EditChildrenDialog
{
    private readonly MainWindowContext _window;

    private bool _isOpened = false;
    private string _name = string.Empty;
    private List<StageObj>[] _selectedObjs = [new(),new()]; // General / Children
    private List<StageObj> _newChildren = new();
    private StageObj _parent;
    private List<StageObj> _oldChildren = new();
    private IEnumerable<SceneObj> sceneObjs;
    private string search = "";

    Vector2 dimensions = new(800, 450);
    public EditChildrenDialog(MainWindowContext window, StageObj stageObj)
    {
        Reset();
        dimensions *= window.ScalingFactor;
        _window = window;
        _parent = stageObj;
        if (_parent.Children != null)
        {
            _oldChildren = _parent.Children;
            _newChildren = new(_parent.Children);
        }
        sceneObjs = _window.CurrentScene!.EnumerateSceneObjs();
        _name = _parent.Name;
    }



    public void Open() => _isOpened = true;

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        dimensions = new(800, 450);
        _name = string.Empty;
        _parent = null;
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
        ImGui.OpenPopup("Modify "+_name+"'s children.");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Modify "+_name+"'s children.",
                ref _isOpened
            )
        )
            return;
        dimensions = ImGui.GetWindowSize();
        ImGui.InputText("SEARCHBOX", ref search, 100);
        Vector2 tableDimensions = new(dimensions.X / 2 - 25, dimensions.Y-96);
        if (ImGui.BeginChild("LEFT", tableDimensions))
        {

            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("Scene objects:");
            ImGui.SetWindowFontScale(1.0f);
            if (ImGui.BeginTable("ObjectTable", 2,
                                                    ImGuiTableFlags.RowBg
                                                    | ImGuiTableFlags.Resizable
                                                | ImGuiTableFlags.BordersOuter
                                                | ImGuiTableFlags.BordersV
                                                | ImGuiTableFlags.ScrollY, new(tableDimensions.X - 2,tableDimensions.Y-30)))
            {
                int i = 0;
                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.None, 0.60f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.40f);
                ImGui.TableHeadersRow();
                foreach (SceneObj obj in sceneObjs)
                {
                    StageObj stageObj = obj.StageObj;
                    if (search != "" && !stageObj.Name.ToLower().Contains(search.ToLower())) continue;
                    if (!(!_newChildren.Contains(stageObj) && _oldChildren.Contains(stageObj)))
                    {
                        if (stageObj == _parent.Parent || _newChildren.Contains(stageObj) || (stageObj.Type != Enums.StageObjType.Regular &&stageObj.Type != Enums.StageObjType.Area ) || (stageObj.Type == Enums.StageObjType.Area && stageObj.Name != "ObjectChildArea") || stageObj.FileType != _parent.FileType || stageObj == _parent)
                            continue;
                    }
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushID("ObjectSelect" + i);
                    bool contained = _selectedObjs[0].Contains(stageObj);
                    if (ImGui.Selectable(stageObj.Name, contained, ImGuiSelectableFlags.DontClosePopups | ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (contained) _selectedObjs[0].Remove(stageObj);
                        else _selectedObjs[0].Add(stageObj);
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            AxisAlignedBoundingBox aabb = obj.Actor.AABB * stageObj.Scale;
                            _window.CurrentScene!.Camera.LookFrom(stageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
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

            ImGui.EndChild();
        }
        ImGui.SameLine(default, 5f);

        if (ImGui.BeginChild("MIDDLE", new(20, tableDimensions.Y)))
        {
            ImGui.Dummy(new(0, tableDimensions.Y / 3));
            if (ImGui.ArrowButton("r", ImGuiDir.Right))
            {
                MoveToChildren();
            }
            ImGui.Dummy(new(0, tableDimensions.Y / 6));

            if (ImGui.ArrowButton("l", ImGuiDir.Left))
            {
                MoveToGeneral();
            }
            ImGui.EndChild();
        }
        ImGui.SameLine();

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
                                                | ImGuiTableFlags.ScrollY, new(tableDimensions.X - 3, tableDimensions.Y-30)))
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
                    if (ImGui.Selectable(stageObj.Name, contained, ImGuiSelectableFlags.DontClosePopups | ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (contained) _selectedObjs[1].Remove(stageObj);
                        else _selectedObjs[1].Add(stageObj);
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            AxisAlignedBoundingBox aabb = new(2);
                            _window.CurrentScene!.Camera.LookFrom(stageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.SetTooltip("Child: " + stageObj.Name);
                        ImGui.EndTooltip();
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(stageObj.Type.ToString());
                    i++;
                }
                ImGui.EndTable();
            }
            ImGui.EndChild();
        }
        //ImGui.TextColored(new Vector4(1, 0, 0, 1), dimensions.X+", "+ dimensions.Y);
        //ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - 90);
        ImGui.SetCursorPosY(dimensions.Y -ImGui.GetTextLineHeight()-14);
        if (ImGui.Button("Ok", new(80, 0)))
        {
            // finish setting up then reset the dialog
            if (_parent.Children == null)
            {
                _parent.Children = new();
            }
            else
            {
                foreach (StageObj old in _parent.Children)
                {
                    if (!_newChildren.Contains(old))
                    {
                        old.Parent = null;
                        old.Type = old.Type == Enums.StageObjType.Child ? Enums.StageObjType.Regular : Enums.StageObjType.Area; 
                    }
                }
                _parent.Children.Clear();
            }
            for (int i = 0; i < _newChildren.Count(); i++)
            {
                _newChildren[i].Parent = _parent;
                _parent.Children.Add(_newChildren[i]);
                switch (_newChildren[i].Type)
                {
                    case Enums.StageObjType.Regular:
                        _newChildren[i].Type = Enums.StageObjType.Child;
                        break;
                    case Enums.StageObjType.Area:
                        _newChildren[i].Type = Enums.StageObjType.AreaChild;
                        break;
                }
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
        {
            _newChildren.Add(so);
        }
        _selectedObjs[0].Clear();
    }
    private void MoveToGeneral() // Gen <- Children
    {
        foreach (StageObj so in _selectedObjs[1])
        {
            _newChildren.Remove(so);
        }
        _selectedObjs[1].Clear();
    }

}
