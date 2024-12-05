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

    public EditChildrenDialog(MainWindowContext window, StageObj stageObj)
    {
        Reset();
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
        _name = string.Empty;
        _parent = null;
        _selectedObjs[0].Clear();
        _selectedObjs[1].Clear();
    }

    public void Render()
    {

        if (!_isOpened)
            return;

        ImGui.OpenPopup("Modify "+_name+"'s children.");

        Vector2 dimensions = new(800 * _window.ScalingFactor, 450 * _window.ScalingFactor);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Modify "+_name+"'s children.",
                ref _isOpened,
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize
            )
        )
            return;
        Vector2 tableDimensions = new(dimensions.X / 2-25, dimensions.Y-80);
        ImGui.BeginChild("LEFT", tableDimensions);
        ImGui.SetWindowFontScale(1.3f);
        ImGui.Text("Scene objects:");
        ImGui.SetWindowFontScale(1.0f);
        if(ImGui.BeginTable("ObjectTable", 2,
                                                ImGuiTableFlags.RowBg
                                                | ImGuiTableFlags.Resizable
                                            | ImGuiTableFlags.BordersOuter
                                            | ImGuiTableFlags.BordersV
                                            | ImGuiTableFlags.ScrollY, new(tableDimensions.X - 2,dimensions.Y - dimensions.Y/4)))
        {
            int i = 0;
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Object",ImGuiTableColumnFlags.None, 0.60f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.40f);
            ImGui.TableHeadersRow();
            foreach (SceneObj obj in sceneObjs)
            {
                StageObj stageObj = obj.StageObj;

                if (!(!_newChildren.Contains(stageObj) && _oldChildren.Contains(stageObj)))
                {
                    if (stageObj == _parent)
                       { float a = 222;}
                    if (stageObj == _parent.Parent || _newChildren.Contains(stageObj) || (stageObj.Type != Enums.StageObjType.Regular && stageObj.Type != Enums.StageObjType.Area) || stageObj.FileType != _parent.FileType || stageObj == _parent)
                    continue;
                }
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("ObjectSelect"+i);
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
        ImGui.SameLine();


        ImGui.SameLine();

        ImGui.BeginChild("MIDDLE", new(20, dimensions.Y - 80));
        ImGui.Dummy(new(0, tableDimensions.Y/3));
        if(ImGui.Button("->"))
        {
            MoveToChildren();
        }
        ImGui.Dummy(new(0, tableDimensions.Y/6));
        
        if(ImGui.Button("<-"))
        {
            MoveToGeneral();
        }
        ImGui.EndChild();
        ImGui.SameLine();
        
        ImGui.BeginChild("RIGHT", tableDimensions);
        ImGui.SetWindowFontScale(1.3f);
        ImGui.Text("Children:");
        ImGui.SetWindowFontScale(1.0f);
        if(ImGui.BeginTable("ChildrenTable", 2,
                                                ImGuiTableFlags.RowBg
                                                | ImGuiTableFlags.Resizable
                                            | ImGuiTableFlags.BordersOuter
                                            | ImGuiTableFlags.BordersV
                                            | ImGuiTableFlags.ScrollY, new(tableDimensions.X-2,dimensions.Y - dimensions.Y/4)))
        {
            int i = 0;
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Object",ImGuiTableColumnFlags.None, 0.60f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.40f);
            ImGui.TableHeadersRow();
            foreach (StageObj stageObj in _newChildren)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("ChildSelect"+i);
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

        // ImGui.TextColored(new Vector4(1, 0, 0, 1), "Warning text");
        // ImGui.SameLine();
        ImGui.SetCursorPosX(dimensions.X - 85);
        if (ImGui.Button("Ok", new(80, 0)))
        {
            // finish setting up then reset the dialog
            if (_parent.Children == null) _parent.Children = _newChildren;
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
