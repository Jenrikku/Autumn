using System.Diagnostics;
using System.Numerics;
using System.Reflection.Emit;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.GUI;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn;

internal class PropertiesWindow(MainWindowContext window)
{
    private float PROP_WIDTH = 145f;
    private Vector3 mTl = Vector3.Zero;
    private Vector3 mRt = Vector3.Zero;
    private Vector3 mSc = Vector3.Zero;
    private int mView = -1;
    private int mClip = -1;
    private int mCamera = -1;
    private string mLayer = "共通";
    private StageObj multiselector = new();
    public void Render()
    {
        if (!ImGui.Begin("Properties"))
            return;

        if (window.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            ImGui.End();
            return;
        }

        IEnumerable<SceneObj> selectedObjects = window.CurrentScene.SelectedObjects;
        int selectedCount = selectedObjects.Count();

        if (selectedCount < 2 && 
        (multiselector.Layer != mLayer 
        || multiselector.ClippingGroupId != mClip 
        || multiselector.ViewId != mView 
        || multiselector.CameraId != mCamera)
        )
        {
            multiselector = new();
        }
        if (selectedCount <= 0)
        {
            ImGui.TextDisabled("No object is selected.");
            ImGui.End();
            return;
        }

        if (selectedCount == 1)
        {
            // Only one object selected:
            SceneObj sceneObj = selectedObjects.First();
            StageObj stageObj = sceneObj.StageObj;
            string oldName = stageObj.Name;
            ImGui.GetIO().ConfigDragClickToInputText = true;
            ImGui.SetWindowFontScale(1.20f);
            ImGui.Text(stageObj.Type.ToString() + ": ");
            ImGui.SameLine();
            ImGui.Text(oldName);
            ImGui.SetWindowFontScale(1.0f);
            var style = ImGui.GetStyle();
            float prevW = ImGui.GetWindowWidth();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBg));
            if (ImGui.CollapsingHeader("General Info", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                ImGui.BeginChild("gen", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);

                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                //ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3.1f);
                ImGui.PushItemWidth(prevW - PROP_WIDTH);
                InputText("Name", ref stageObj.Name, 128, ref stageObj);
                if (stageObj is not RailObj)
                {
                    if (window.ContextHandler.Settings.UseClassNames)
                    {
                        string hint = string.Empty;

                        if (string.IsNullOrEmpty(stageObj.ClassName))
                            hint = GetClassFromCCNT(stageObj.Name);

                        stageObj.ClassName ??= ""; // So that input text works well.

                        InputText("ClassName", ref stageObj.ClassName, 128, ref stageObj, hint);
                    }
                    else
                    {
                        ImGui.Text("ClassName: ");
                        ImGui.SameLine();
                        ImGui.Text(GetClassFromCCNT(stageObj.Name));
                        ImGui.SameLine();
                        ImGui.Spacing();
                        ImGui.SameLine();

                        ImGuiWidgets.HelpTooltip(
                            "The class name is picked from CreatorClassNameTable.szs"
                        );
                    }
                    ImGui.Text("File type: ");
                    ImGui.SameLine();
                    ImGui.Text(stageObj.FileType.ToString());
                    ImGui.Text("Object type: ");
                    ImGui.SameLine();
                    ImGui.Text(stageObj.Type.ToString());

                }
                InputText("Layer", ref stageObj.Layer, 30, ref stageObj);
                if (stageObj.Type != StageObjType.Start)
                {
                    InputInt("ViewId", ref stageObj.ViewId, 1, ref stageObj);
                    InputInt("CameraId", ref stageObj.CameraId, 1, ref stageObj);
                    InputInt("ClippingGroupId", ref stageObj.ClippingGroupId, 1, ref stageObj);
                }
                ImGui.PopItemWidth();
                //ImGui.PopStyleVar();
                ImGui.EndChild();
            }

            if (ImGui.CollapsingHeader("Object Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                ImGui.BeginChild("trl", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                //ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2.1f);
                ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);
                DragFloat3("Translation", ref stageObj.Translation, v_speed: 10, ref sceneObj);
                DragFloat3("Rotation", ref stageObj.Rotation, v_speed: 2, ref sceneObj);
                DragFloat3("Scale", ref stageObj.Scale, v_speed: 0.2f, ref sceneObj);
                ImGui.PopItemWidth();
                ImGui.EndChild();
            }

            if (stageObj.Type == StageObjType.Regular || stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.Child || stageObj.Type == StageObjType.AreaChild)
            {
                if (ImGui.CollapsingHeader("Object Arguments", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("arg", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                    ImGui.PopStyleVar();
                    //ImGui.GetWindowWidth() - style.WindowPadding.X - 
                    // prevW - PROP_WIDTH
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);
                    foreach (var (name, property) in stageObj.Properties)
                    {
                        if (property is null)
                        {
                            ImGui.TextDisabled(name);
                            return;
                        }
                        if (!name.Contains("Arg")) continue;

                        switch (property)
                        {
                            case object p when p is int:
                                int intBuf = (int)(p ?? -1);
                                InputIntProperties(name, ref intBuf, 1, ref stageObj);

                                break;

                            case object p when p is string:
                                string strBuf = (string)(p ?? string.Empty);
                                InputTextProperties(name, ref strBuf, 128, ref stageObj);
                                break;

                            default:
                                throw new NotImplementedException(
                                    "The property type " + property?.GetType().FullName
                                        ?? "null" + " is not supported."
                                );
                        }
                    }
                    ImGui.PopItemWidth();
                    ImGui.EndChild();
                }
            }
            if (stageObj.Type == StageObjType.Goal || stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.CameraArea || stageObj.Type == StageObjType.Regular || stageObj.Type == Enums.StageObjType.Child || stageObj.Type == Enums.StageObjType.AreaChild)
            {
                if (ImGui.CollapsingHeader("Object Switches", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("swc", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                    ImGui.PushItemWidth(prevW - PROP_WIDTH);
                    if (stageObj.SwitchA != null) 
                        InputInt("SwitchA", ref stageObj.SwitchA, 1, ref stageObj);
                    if (stageObj.SwitchB != null) InputInt("SwitchB", ref stageObj.SwitchB, 1, ref stageObj);
                    if (stageObj.SwitchAppear != null) InputInt("SwitchAppear", ref stageObj.SwitchAppear, 1, ref stageObj);
                    if (stageObj.SwitchDeadOn != null) InputInt("SwitchDeadOn", ref stageObj.SwitchDeadOn, 1, ref stageObj);
                    if (stageObj.SwitchKill != null) InputInt("SwitchKill", ref stageObj.SwitchKill, 1, ref stageObj);
                    ImGui.PopItemWidth();
                    ImGui.EndChild();
                }
                if (stageObj.Type != StageObjType.CameraArea && stageObj.Type != StageObjType.Goal)
                {
                    if (ImGui.CollapsingHeader("Object Relations", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                        if (ImGui.BeginChild("pnt", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.Text("Parent: ");
                            ImGui.SameLine();
                            if (stageObj.Parent != null)
                            {
                                ImGui.GetColorU32(ImGuiCol.NavHighlight);
                                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.HeaderActive));
                                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                                ImGui.Text(stageObj.Parent.Name);
                                ImGui.PopStyleColor();
                                if (ImGui.IsItemClicked())
                                {
                                    foreach (SceneObj s in window.CurrentScene.EnumerateSceneObjs())
                                    {
                                        if (s.StageObj == stageObj.Parent)
                                        {
                                            ChangeHandler.ToggleObjectSelection(
                                                window,
                                                window.CurrentScene.History,
                                                s.PickingId,
                                                typeof(SceneObj),
                                                !(window.Keyboard?.IsCtrlPressed() ?? false)
                                                );
                                            AxisAlignedBoundingBox aabb = s.Actor.AABB * s.StageObj.Scale;
                                            window.CurrentScene!.Camera.LookFrom(s.StageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                ImGui.TextDisabled("No parent assigned.");
                            }
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.Text("Children: ");
                            if (stageObj.Children != null && stageObj.Children.Any())
                            {
                                ImGui.SameLine();
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
                                if (ImGui.Button("Edit children", new Vector2(ImGui.GetWindowWidth() - ImGui.GetCursorPosX() - style.WindowPadding.X, default)))
                                {
                                    window._editChildrenDialog = new(window, stageObj);
                                    window._editChildrenDialog.Open();
                                }
                                bool autoResize = stageObj.Children.Count < 6;
                                if (ImGui.BeginTable("childrenTable", 2,
                                    ImGuiTableFlags.RowBg
                                    | ImGuiTableFlags.BordersOuter
                                    | ImGuiTableFlags.BordersV
                                    | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - style.WindowPadding.X, autoResize ? default : 150 * window.ScalingFactor)))
                                {
                                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                                    ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None);
                                    ImGui.TableHeadersRow();
                                    if (autoResize) ImGui.SetScrollY(0);
                                    foreach (SceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
                                    {
                                        StageObj sObj = obj.StageObj;

                                        if (sObj.Type != Enums.StageObjType.Child && sObj.Type != Enums.StageObjType.AreaChild) continue;
                                        if (sObj.Parent != stageObj) continue;
                                        ImGui.TableNextRow();

                                        ImGui.TableSetColumnIndex(0);

                                        ImGui.PushID("SceneChildSelectable" + obj.PickingId);
                                        if (ImGui.Selectable(sObj.Name, false, ImGuiSelectableFlags.SpanAllColumns))
                                        {
                                            ChangeHandler.ToggleObjectSelection(
                                                window,
                                                window.CurrentScene.History,
                                                obj,
                                                !window.Keyboard?.IsCtrlPressed() ?? true
                                            );
                                            AxisAlignedBoundingBox aabb = obj.Actor.AABB * sObj.Scale;
                                            window.CurrentScene!.Camera.LookFrom(sObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                                        }

                                        ImGui.TableNextColumn();

                                        ImGui.Text(sObj.Type.ToString());
                                    }

                                    ImGui.EndTable();
                                }
                            }
                            else
                            {
                                ImGui.SameLine();
                                if (ImGui.Button("Add children", new Vector2(ImGui.GetWindowWidth() - ImGui.GetCursorPosX() - style.WindowPadding.X, default)))
                                {
                                    window._editChildrenDialog = new(window, stageObj);
                                    window._editChildrenDialog.Open();
                                }
                            }

                        }
                        ImGui.EndChild();
                    }

                }
            }

            if (stageObj.Type == Enums.StageObjType.Regular || stageObj.Type == StageObjType.Child)
            {
                if (ImGui.CollapsingHeader("Rail", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("rl", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    //ImGui.SetCursorPosY(ImGui.GetCursorPosY() + style.FramePadding.Y);
                    if (stageObj.Rail is not null)
                    {
                        ImGui.Text("Rail: " + stageObj.Rail.Name);
                    }
                    else
                    {
                        ImGui.Text("Rail: ");
                        ImGui.SameLine();
                        ImGui.TextDisabled("No rail assigned.");
                    }
                    ImGui.EndChild();
                }
            }

            if (ImGui.CollapsingHeader("Misc. Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                ImGui.BeginChild("prp", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                //ImGui.SetCursorPosY(ImGui.GetCursorPosY() + style.FramePadding.Y);
                ImGui.PushItemWidth(prevW - PROP_WIDTH);
                foreach (var (name, property) in stageObj.Properties)
                {
                    if (property is null)
                    {
                        ImGui.TextDisabled(name);
                        return;
                    }
                    if (name.Contains("Arg")) continue;

                    switch (property)
                    {
                        case object p when p is int:
                            int intBuf = (int)(p ?? -1);
                            InputIntProperties(name, ref intBuf, 1, ref stageObj);
                            break;

                        case object p when p is string:
                            string strBuf = (string)(p ?? string.Empty);
                            InputTextProperties(name, ref strBuf, 128, ref stageObj);
                            break;

                        default:
                            throw new NotImplementedException(
                                "The property type " + property?.GetType().FullName
                                    ?? "null" + " is not supported."
                            );
                    }
                }
                if (ImGui.Button("Edit Misc Properties", new(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X, default)))
                {
                    if (!stageObj.Properties.ContainsKey("ShapeModelNo"))
                    {
                        stageObj.Properties.Add("ShapeModelNo", 0);
                    }
                }
                ImGui.PopItemWidth();
                ImGui.EndChild();
            }


            if (oldName != stageObj.Name)
            {
                sceneObj.UpdateActor(window.ContextHandler.FSHandler, window.GLTaskScheduler);
            }

            ImGui.PopStyleColor();
        }
        else
        {
            // Multiple objects selected:
            ImGui.TextDisabled("Multiple objects selected.");
            InputText("Layer", ref multiselector.Layer, 30, ref multiselector);
            InputInt("ViewId", ref multiselector.ViewId , 1, ref multiselector);
            InputInt("CameraId", ref multiselector.CameraId, 1, ref multiselector);
            InputInt("ClippingGroupId", ref multiselector.ClippingGroupId, 1, ref multiselector);
            foreach (SceneObj sceneObj in window.CurrentScene.SelectedObjects)
            {
                if (multiselector.ViewId != mView)
                    sceneObj.StageObj.ViewId = multiselector.ViewId;
                if (multiselector.CameraId != mCamera)
                    sceneObj.StageObj.CameraId = multiselector.CameraId;
                if (multiselector.ClippingGroupId != mClip)
                    sceneObj.StageObj.ClippingGroupId = multiselector.ClippingGroupId;
                if (multiselector.Layer != mLayer)
                    sceneObj.StageObj.Layer = multiselector.Layer;
            }

        }

        ImGui.End();
    }

    #region Undoable Actions
    private bool DragFloat3(string str, ref Vector3 rf, float v_speed, ref SceneObj sto)
    {
        Vector3 v = rf;
        if (ImGui.InputFloat3(str, ref v, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeTransform(window.CurrentScene.History, sto, str, rf, v);
            return true;
        }
        return false;
    }
    private bool InputText(string str, ref string rf, uint max, ref StageObj sto, string? hint = "")
    {
        string s = rf;
        if (ImGui.InputTextWithHint(str, hint, ref s, max, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeFieldValue(window.CurrentScene.History, sto, str, rf, s);
        }
        return false;
    }
    private bool InputInt(string str, ref int rf, int step, ref StageObj sto)
    {
        int i = rf;
        if (ImGui.InputInt(str, ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeFieldValue(window.CurrentScene.History, sto, str, rf, i);
        }
        return false;
    }
    private bool InputTextProperties(string str, ref string rf, uint max, ref StageObj sto)
    {
        string s = rf;
        if (ImGui.InputText(str, ref s, max, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene.History, sto.Properties, str, rf, s);
        }
        return false;
    }
    private bool InputIntProperties(string str, ref int rf, int step, ref StageObj sto)
    {
        int i = rf;
        if (ImGui.InputInt(str, ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene.History, sto.Properties, str, rf, i);
        }
        return false;
    }

    private string GetClassFromCCNT(string objectName)
    {
        var table = window.ContextHandler.FSHandler.ReadCreatorClassNameTable();

        if (!table.TryGetValue(objectName, out string? className))
            return "NotFound";

        return className;
    }
    #endregion
}
