using System.Numerics;
using Autumn.Enums;
using Autumn.GUI;
using Autumn.GUI.Windows;
using Autumn.Rendering.Area;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn;

internal class PropertiesWindow(MainWindowContext window)
{
    private const float PROP_WIDTH = 145f;
    private Vector3 mTl = Vector3.Zero;
    private Vector3 mRt = Vector3.Zero;
    private Vector3 mSc = Vector3.Zero;
    private int mView = -1;
    private int mClip = -1;
    private int mCamera = -1;
    private string mLayer = "共通";
    private StageObj multiselector = new();
    DragFloat3 PosDrag = new(window);
    DragFloat3 RotDrag = new(window);
    LinkedDragFloat3 ScaleDrag = new(window);
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.AutoHideTabBar }; //ImGuiWidgets.NO_TAB_BAR };
    
    public void Render()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x00000000);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x00000000);
        unsafe 
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        bool begun = ImGui.Begin("Properties");
        ImGui.PopStyleColor(3);
        
        if (!begun)
            return;
        if (window.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            ImGui.End();
            return;
        }

        IEnumerable<ISceneObj> selectedObjects = window.CurrentScene.SelectedObjects;
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
            ISceneObj sceneObj = selectedObjects.First();
            StageObj stageObj = sceneObj.StageObj;
            string oldName = stageObj.Name;
            ImGui.GetIO().ConfigDragClickToInputText = true;
            ImGui.SetWindowFontScale(1.20f);

            // Fake dock
            bool usedbName = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(GetClassFromCCNT(oldName)) && ClassDatabaseWrapper.DatabaseEntries[GetClassFromCCNT(oldName)].Name != null;
            ImGui.SetCursorPosY(ImGui.GetCursorPosY()-4);
            ImGui.Text(stageObj.Type.ToString() + ": " + (usedbName ? ClassDatabaseWrapper.DatabaseEntries[GetClassFromCCNT(oldName)].Name : oldName));
            ImGui.SetWindowFontScale(1.0f);

            var ypos = ImGui.GetCursorPosY();
            bool scrollToChild = false;
            if (stageObj.Parent is not null)
            {
                ImGui.SameLine(ImGui.GetWindowWidth() - 24 - 12);
                if (ImGui.Button("P", new(20, default)))
                {
                    window.ContextHandler.ActionHandler.ExecuteAction(CommandID.GotoParent, window);
                }
            }
            else if (stageObj.Children != null && stageObj.Children.Count > 0)
            {
                ImGui.SameLine(ImGui.GetWindowWidth() - 24 - 12);
                if (ImGui.Button("C", new(20, default)))
                {
                    if (stageObj.Children.Count == 1)
                    {
                        window.ContextHandler.ActionHandler.ExecuteAction(CommandID.GotoParent, window);
                    }
                    else
                        scrollToChild = true;
                }
            }
            ImGui.SetCursorPosY(ypos);
            ImGui.Separator();
            //ImGui.Separator();
            var style = ImGui.GetStyle();
            float prevW = ImGui.GetWindowWidth();

            ImGui.SetNextItemWidth(prevW + 200);
            if (ImGui.BeginChild("PropertiesReal", new(prevW - style.ItemSpacing.X, default)))
            {
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
                            ImGui.SetItemTooltip(GetClassFromCCNT(stageObj.Name));
                            ImGui.BeginDisabled();
                            var s = "";
                            InputText("ClassName", ref s, 128, ref stageObj, GetClassFromCCNT(stageObj.Name));
                            ImGui.EndDisabled();
                        }
                        //ImGui.Text("File type: " + stageObj.FileType);
                        //ImGui.Text("Object type: "+ stageObj.Type);

                    }
                    InputText("Layer", ref stageObj.Layer, 30, ref stageObj);
                    if (stageObj.Type != StageObjType.Start)
                    {
                        InputInt("ViewId", ref stageObj.ViewId, 1, ref stageObj);
                        InputInt("CameraId", ref stageObj.CameraId, 1, ref stageObj);
                        InputInt("ClippingGroupId", ref stageObj.ClippingGroupId, 1, ref stageObj);
                    }
                    if (stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.CameraArea)
                    {
                        int prior = !stageObj.Properties.ContainsKey("ShapeModelNo") ? -989 : (int)stageObj.Properties["Priority"]!;
                        InputIntProperties("Priority", ref prior, 1, ref stageObj);

                        int shp = !stageObj.Properties.ContainsKey("ShapeModelNo") ? 989 : (int)stageObj.Properties["ShapeModelNo"]!;
                        InputIntProperties("ShapeModelNo", ref shp, 1, ref stageObj);
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
                    PosDrag.Use("Position", ref stageObj.Translation, ref sceneObj, 10);
                    RotDrag.Use("Rotation", ref stageObj.Rotation, ref sceneObj, 0.5f);
                    ScaleDrag.Use("Scale", ref stageObj.Scale, ref sceneObj, 0.01f, style);
                    ImGui.EndChild();
                }

                if (stageObj.Type == StageObjType.Regular || stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.Child || stageObj.Type == StageObjType.AreaChild || stageObj.Type == StageObjType.Goal || stageObj.Type == StageObjType.DemoScene)
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
                            string cls = GetClassFromCCNT(stageObj.Name);
                            if (!ClassDatabaseWrapper.DatabaseEntries.ContainsKey(cls))
                            {
                                switch (property)
                                {
                                    case int:
                                        int intBuf = (int)(property ?? -1);
                                        InputIntProperties(name, ref intBuf, 1, ref stageObj);

                                        break;

                                    case string:
                                        string strBuf = (string)(property ?? string.Empty);
                                        InputTextProperties(name, ref strBuf, 128, ref stageObj);
                                        break;

                                    case float:
                                        float flBuf = (float)(property ?? -1);
                                        InputFloatProperties(name, ref flBuf, 1, ref stageObj);
                                        break;

                                    default:
                                        throw new NotImplementedException(
                                            "The property type " + property?.GetType().FullName
                                                ?? "null" + " is not supported."
                                        );
                                }
                            }
                            else
                            {
                                switch (property)
                                {
                                    case int:
                                        int intBuf = (int)(property ?? -1);
                                        if (ClassDatabaseWrapper.DatabaseEntries[cls].Args != null && ClassDatabaseWrapper.DatabaseEntries[cls].Args.ContainsKey(name))
                                        {
                                            var argEntry = ClassDatabaseWrapper.DatabaseEntries[cls].Args[name];
                                            ImGui.Text(argEntry.Name + ":");
                                            if (argEntry.Type == "enum")
                                            {
                                                var rf = argEntry.Values.Keys.ToList().IndexOf(intBuf);
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(argEntry.Name);
                                                if (rf < 0)
                                                {
                                                    rf = intBuf;
                                                    ImGui.InputInt("##" + argEntry.Name, ref rf, 1, default);
                                                    if (intBuf != rf)
                                                    {
                                                        stageObj.Properties[name] = rf;
                                                    }
                                                }
                                                else
                                                {
                                                    ImGui.Combo("##" + argEntry.Name + "c",
                                                    ref rf,
                                                    argEntry.Values.Values.ToArray(),
                                                    argEntry.Values.Count
                                                    );
                                                    ImGui.SetItemTooltip(argEntry.Values[argEntry.Values.Keys.ElementAt(rf)]);
                                                    if (intBuf != argEntry.Values.Keys.ElementAt(rf))
                                                    {
                                                        stageObj.Properties[name] = argEntry.Values.Keys.ElementAt(rf);
                                                    }
                                                }
                                            }
                                            else if (argEntry.Type == "bool")
                                            {
                                                var rf = intBuf != -1;
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(argEntry.Name);
                                                ImGui.Checkbox("##" + argEntry.Name + "cb", ref rf);
                                                if ((intBuf != -1) != rf)
                                                {
                                                    stageObj.Properties[name] = rf ? 1 : -1;
                                                }
                                            }
                                            else // if (argEntry.Type is null || argEntry.Type == "int")
                                            {
                                                var rf = intBuf;
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(argEntry.Name);
                                                ImGui.InputInt("##" + argEntry.Name + "i", ref rf, 1, default);
                                                int.Clamp(rf, argEntry.Min ?? -99999, argEntry.Max ?? 99999);
                                                if (intBuf != rf)
                                                {
                                                    stageObj.Properties[name] = rf;
                                                }
                                            }
                                        }
                                        else
                                            InputIntProperties(name, ref intBuf, 1, ref stageObj);

                                        break;

                                    case string:
                                        string strBuf = (string)(property ?? string.Empty);
                                        InputTextProperties(name, ref strBuf, 128, ref stageObj);
                                        break;

                                    case float:
                                        float flBuf = (float)(property ?? -1);
                                        InputFloatProperties(name, ref flBuf, 1, ref stageObj);
                                        break;

                                    default:
                                        throw new NotImplementedException(
                                            "The property type " + property?.GetType().FullName
                                                ?? "null" + " is not supported."
                                        );
                                }
                            }
                        }
                        ImGui.PopItemWidth();
                        ImGui.EndChild();
                    }
                }
                if (stageObj.Type == StageObjType.Goal || stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.CameraArea || stageObj.Type == StageObjType.Regular || stageObj.Type == StageObjType.Child || stageObj.Type == Enums.StageObjType.AreaChild || stageObj.Type == StageObjType.DemoScene)
                {
                    if (ImGui.CollapsingHeader("Object Switches", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                        ImGui.BeginChild("swc", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                        ImGui.PushItemWidth(prevW - PROP_WIDTH);
                        InputSwitch("A", ref stageObj.SwitchA, 1, ref sceneObj);
                        InputSwitch("B", ref stageObj.SwitchB, 1, ref sceneObj);
                        InputSwitch("Appear", ref stageObj.SwitchAppear, 1, ref sceneObj);
                        InputSwitch("DeadOn", ref stageObj.SwitchDeadOn, 1, ref sceneObj);
                        InputSwitch("Kill", ref stageObj.SwitchKill, 1, ref sceneObj);
                        ImGui.PopItemWidth();
                        ImGui.EndChild();
                    }
                    if (stageObj.Type != StageObjType.CameraArea && stageObj.Type != StageObjType.Goal)
                    {
                        if (scrollToChild)
                        {
                            ImGui.SetScrollY(ImGui.GetCursorPosY());
                            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetColorU32(ImGuiCol.HeaderActive));
                        }
                        if (ImGui.CollapsingHeader("Object Relations", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            if (ImGui.BeginChild("pnt", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                                ImGui.Text("Parent:");
                                ImGui.SameLine();
                                string pName = stageObj.Parent is not null ? stageObj.Parent.Name : "No parent";
                                if (stageObj.Parent == null)
                                    ImGui.BeginDisabled();
                                if (ImGui.Button(pName, new(ImGuiWidgets.SetPropertyWidth("Parent") - ImGui.CalcTextSize("\uf127").X * 1.65f * window.ScalingFactor , default)))
                                {
                                    var p = window.CurrentScene.GetSceneObjFromStageObj(stageObj.Parent!);
                                    ChangeHandler.ToggleObjectSelection(
                                                window,
                                                window.CurrentScene.History,
                                                p.PickingId,
                                                !(window.Keyboard?.IsCtrlPressed() ?? false)
                                            );
                                    AxisAlignedBoundingBox aabb = p.AABB * p.StageObj.Scale;
                                    window.CurrentScene!.Camera.LookFrom(
                                        p.StageObj.Translation * 0.01f,
                                        aabb.GetDiagonal() * 0.01f
                                    );
                                }

				                ImGui.SameLine(default, style.ItemSpacing.X / 2);
                                if (ImGui.Button("\uf127##" + pName))
                                {
                                    window.CurrentScene.Stage.GetStageFile(StageFileType.Map).UnlinkChild(stageObj);
                                }

                                ImGui.SetItemTooltip("Unlink Parent");
                                if (stageObj.Parent == null)
                                    ImGui.EndDisabled();
                                ImGui.Text("Children: ");
                                if (stageObj.Children != null && stageObj.Children.Any())
                                {
                                    ImGui.SameLine();
                                    if (ImGui.Button("Edit children", new Vector2(ImGuiWidgets.SetPropertyWidth("Parent:"), default)))
                                    {
                                        window.SetupChildrenDialog(stageObj);
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
                                        int cidx = 0;
                                        foreach (StageObj ch in stageObj.Children) // This keeps child order!
                                        {
                                            ImGui.TableNextRow();

                                            ImGui.TableSetColumnIndex(0);

                                            ImGui.PushID("SceneChildSelectable" + cidx);
                                            if (ImGui.Selectable(ch.Name, false, ImGuiSelectableFlags.SpanAllColumns))
                                            {
                                                var child = window.CurrentScene.GetSceneObjFromStageObj(ch);
                                                ChangeHandler.ToggleObjectSelection(
                                                    window,
                                                    window.CurrentScene.History,
                                                    child.PickingId,
                                                    !window.Keyboard?.IsCtrlPressed() ?? true
                                                );
                                                AxisAlignedBoundingBox aabb = child.AABB * ch.Scale;
                                                window.CurrentScene!.Camera.LookFrom(ch.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                                            }

                                            ImGui.TableNextColumn();

                                            ImGui.Text(ch.Type.ToString());
                                            cidx++;
                                        }

                                        ImGui.EndTable();
                                    }
                                }
                                else
                                {
                                    ImGui.SameLine();
                                    if (ImGui.Button("Add children", new Vector2(ImGuiWidgets.SetPropertyWidth("Children:"), default)))
                                    {
                                        window.SetupChildrenDialog(stageObj);
                                    }
                                }
                            }
                            ImGui.EndChild();
                        }

                        if (scrollToChild)
                        {
                            ImGui.PopStyleColor();
                        }
                    }
                }

                if (stageObj.Type == StageObjType.Regular || stageObj.Type == StageObjType.Child)
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

                if (ImGui.CollapsingHeader("General Properties", ImGuiTreeNodeFlags.DefaultOpen))
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
                        if (name.Contains("Arg") || name == "Priority" || name == "ShapeModelNo") continue;

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
                    if (sceneObj is ActorSceneObj actorSceneObj)
                    {
                        actorSceneObj.UpdateActor(window.ContextHandler.FSHandler, window.GLTaskScheduler);
                    }

                    if (sceneObj is BasicSceneObj basicSceneObj && sceneObj.StageObj.IsArea())
                    {
                        basicSceneObj.MaterialParams.Color = AreaMaterial.GetAreaColor(stageObj.Name);
                    }
                }

                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
        }
        else
        {
            // Multiple objects selected:
            ImGui.TextDisabled("Multiple objects selected.");
            InputText("Layer", ref multiselector.Layer, 30, ref multiselector);
            InputInt("ViewId", ref multiselector.ViewId, 1, ref multiselector);
            InputInt("CameraId", ref multiselector.CameraId, 1, ref multiselector);
            InputInt("ClippingGroupId", ref multiselector.ClippingGroupId, 1, ref multiselector);

            foreach (ISceneObj sceneObj in window.CurrentScene.SelectedObjects)
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

    private class CustomDragFloat
    {
        bool isActive = false;
        bool isEditing = false;
        bool isDragging = false;
        bool isFinished = true;
        Vector2 min, max = new();
        public float reference = 0.723764823f;

        public bool Use(string str, ref float rf, ImGuiStylePtr style, float v_speed = 1, bool isSingle = true)
        {
            if (rf != reference && isFinished)
            {
                reference = rf;
            }

            min = ImGui.GetCursorPos() + ImGui.GetWindowPos();
            ImGui.DragFloat(isSingle ? str : "##" + str, ref reference, v_speed, default, default, "%.2f"); // C printf formatting
            max = min + ImGui.GetItemRectSize();
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (ImGui.IsMouseHoveringRect(min, max) && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    isActive = true;
                    isDragging = false;
                    isFinished = false;
                }
            }
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && isActive && !isDragging)
            {
                isDragging = true;
                isEditing = false;
                isFinished = false;
            }
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                // if we release on top of the object we enter editing mode
                // if we release elsewhere while we're dragging we push the change value (true)
                if (ImGui.IsMouseHoveringRect(min, max) && !isDragging)
                {
                    isEditing = true;
                }
                else if (!isDragging)
                {
                    isActive = false;
                }
                else if (isDragging)
                {
                    Complete(ref rf);
                    return true;
                }
            }
            if (isEditing)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Tab) || ImGui.IsKeyPressed(ImGuiKey.Enter) || !isActive)
                {
                    Complete(ref rf);
                    return true;
                }
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                Reset(ref rf);
                return true;
            }
            return false;
        }

        void Complete(ref float rf)
        {
            isEditing = false;
            isActive = false;
            isDragging = false;
            isFinished = true;
            rf = reference;
        }
        void Reset(ref float rf)
        {
            isEditing = false;
            isActive = false;
            isDragging = false;
            isFinished = true;
            reference = rf;
        }

    }

    private class DragFloat3
    {
        CustomDragFloat DragFloatX;
        CustomDragFloat DragFloatY;
        CustomDragFloat DragFloatZ;
        Vector3 refVec3;
        MainWindowContext _window;
        public DragFloat3(MainWindowContext window)
        {
            _window = window;
            DragFloatX = new();
            DragFloatY = new();
            DragFloatZ = new();
        }
        public bool Use(string str, ref Vector3 rf, ref ISceneObj sto, float v_speed = 1, float? width = null, bool linked = false)
        {
            if (refVec3 != rf)
            {
                refVec3 = rf;
            }
            var style = ImGui.GetStyle();
            bool validate = false;

            float stringWidth = ImGui.CalcTextSize(str + ":").X;
            float itemWidth;// = width ?? (ImGui.GetWindowWidth() - stringWidth) / 3 - style.ItemSpacing.X * 2 - 4;

            if (width == null)
            {
                ImGui.Text(str + ":");
                ImGui.SameLine(default, style.ItemSpacing.X);
                if (ImGui.GetWindowWidth() - (ImGui.GetWindowWidth() * 3 / 4 - ImGui.GetStyle().ItemSpacing.X / 2) > (stringWidth + 12))
                {
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() / 4);
                    itemWidth = ImGui.GetWindowWidth() * 3 / 4 - ImGui.GetStyle().ItemSpacing.X / 2 - 24* _window.ScalingFactor;
                }
                else
                {
                    itemWidth = ImGui.GetWindowWidth() - stringWidth - ImGui.GetStyle().ItemSpacing.X * 2 - 24* _window.ScalingFactor;
                }
            }
            else
            {
                ImGui.Text("");
                ImGui.SameLine(default, style.ItemSpacing.X);
                itemWidth = (float)width;
            }
            //ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 0);
            itemWidth = itemWidth / 3 - style.ItemSpacing.X - 7;

            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF_04_04_6C); // NEEDS CONSTANT
            if (ImGui.BeginChild(str + "XTest", new(20* _window.ScalingFactor, 20* _window.ScalingFactor + style.ItemSpacing.Y)))
            {
                ImGui.SetCursorPos(ImGui.GetWindowSize() / 2 - ImGui.CalcTextSize("X") / 2);
                ImGui.Text("X");
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.SameLine(default, 0);
            ImGui.SetNextItemWidth(itemWidth);
            if (DragFloatX.Use(str + "X", ref refVec3.X, style, v_speed, false))
                validate = true;


            ImGui.SameLine(default, style.ItemSpacing.X / 2);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF_15_6C_15); // NEEDS CONSTANT
            if (ImGui.BeginChild(str + "YTest", new(20* _window.ScalingFactor, 20* _window.ScalingFactor + style.ItemSpacing.Y)))
            {
                ImGui.SetCursorPos(ImGui.GetWindowSize() / 2 - ImGui.CalcTextSize("Y") / 2);
                ImGui.Text("Y");
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.SameLine(default, 0);
            ImGui.SetNextItemWidth(itemWidth);
            if (DragFloatY.Use(str + "Y", ref refVec3.Y, style, v_speed, false))
                validate = true;


            ImGui.SameLine(default, style.ItemSpacing.X / 2);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF_6C_27_15); // NEEDS CONSTANT
            if (ImGui.BeginChild(str + "ZTest", new(20* _window.ScalingFactor, 20* _window.ScalingFactor + style.ItemSpacing.Y)))
            {
                ImGui.SetCursorPos(ImGui.GetWindowSize() / 2 - ImGui.CalcTextSize("Z") / 2);
                ImGui.Text("Z");
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.SameLine(default, 0);

            ImGui.SetNextItemWidth(itemWidth);
            if (DragFloatZ.Use(str + "Z", ref refVec3.Z, style, v_speed, false))
                validate = true;



            var rV = new Vector3(DragFloatX.reference, DragFloatY.reference, DragFloatZ.reference);
            if (rV != rf)
            {

                if (linked)
                {
                    if (rV.X != rf.X)
                    {
                        rV = new(rV.X);
                    }
                    else if (rV.Y != rf.Y)
                    {
                        rV = new(rV.Y);
                    }
                    else if (rV.Z != rf.Z)
                    {
                        rV = new(rV.Z);
                    }
                }
                var tmprf = rf;
                switch (str)
                {
                    case "Position":
                    case "Translation":
                        sto.StageObj.Translation = rV;
                        sto.UpdateTransform();
                        sto.StageObj.Translation = tmprf;
                        break;

                    case "Rotation":
                        sto.StageObj.Rotation = rV;
                        sto.UpdateTransform();
                        sto.StageObj.Rotation = tmprf;
                        break;

                    case "Scale":
                        sto.StageObj.Scale = rV;
                        sto.UpdateTransform();
                        sto.StageObj.Scale = tmprf;
                        break;

                }
            }
            if (validate)
            {
                if (linked)
                {
                    if (refVec3.X != rf.X)
                    {
                        refVec3 = new(refVec3.X);
                    }
                    else if (refVec3.Y != rf.Y)
                    {
                        refVec3 = new(refVec3.Y);
                    }
                    else if (refVec3.Z != rf.Z)
                    {
                        refVec3 = new(refVec3.Z);
                    }
                }
                if (str == "Position") str = "Translation";
                ChangeHandler.ChangeTransform(_window.CurrentScene.History, sto, str, rf, refVec3);
                return true;
            }
            return false;
        }
    }

    private class LinkedDragFloat3(MainWindowContext window)
    {
        DragFloat3 ScaleDrag3 = new(window);
        bool isLinked = false;
        public bool Use(string str, ref Vector3 rf, ref ISceneObj sto, float v_speed, ImGuiStylePtr style)
        {
            ImGui.Text(str + ":");
            float stringWidth = ImGui.CalcTextSize(str + ":").X;
            float itemWidth;
            ImGui.SameLine();
            if (ImGui.GetWindowWidth() - (ImGui.GetWindowWidth() * 3 / 4 - style.ItemSpacing.X / 2) > (stringWidth + 12))
            {
                ImGui.SetCursorPosX(ImGui.GetWindowWidth() / 4 - 8);
                itemWidth = ImGui.GetWindowWidth() * 3 / 4 - style.ItemSpacing.X / 2 - 24 - 32;
            }
            else
            {
                itemWidth = ImGui.GetWindowWidth() - stringWidth - style.ItemSpacing.X * 2 - 24 - 32;
            }
            bool ret = ScaleDrag3.Use(str, ref rf, ref sto, v_speed, itemWidth, isLinked);
            ImGui.SameLine(default, style.ItemSpacing.X / 2);
            if (ImGui.Button(isLinked ? "\uf0c1" : "\uf127"))
            {
                isLinked = !isLinked;
            }
            return ret;
        }
    }

    private bool InputText(string str, ref string rf, uint max, ref StageObj sto, string? hint = "")
    {
        string s = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputTextWithHint("##" + str + "i", hint, ref s, max, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeFieldValue(window.CurrentScene?.History!, sto, str, rf, s);
        }

        return false;
    }

    private bool InputInt(string str, ref int rf, int step, ref StageObj sto)
    {
        int i = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputInt("##" + str + "i", ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeFieldValue(window.CurrentScene?.History!, sto, str, rf, i);
        }

        return false;
    }

    private bool InputTextProperties(string str, ref string rf, uint max, ref StageObj sto)
    {
        string s = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputText("##" + str + "i", ref s, max, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, sto.Properties, str, rf, s);
        }

        return false;
    }

    private bool InputIntProperties(string str, ref int rf, int step, ref StageObj sto)
    {
        int i = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputInt("##" + str + "i", ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene.History, sto.Properties, str, rf, i);
        }
        return false;
    }
    private bool InputFloatProperties(string str, ref float rf, int step, ref StageObj sto)
    {
        float i = rf;
        ImGui.Text(str);
        ImGui.SameLine(default, ImGui.CalcTextSize(str).X);
        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.CalcTextSize(str).X - ImGui.GetStyle().ItemSpacing.X * 2);
        if (ImGui.InputFloat("##" + str + "i", ref i, step, default, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, sto.Properties, str, rf, i);
        }

        return false;
    }

    private bool InputSwitch(string str, ref int rf, int step, ref ISceneObj sco)
    {
        int i = rf;
        bool disable = rf < 0;
        if (disable)
            ImGui.BeginDisabled();
        if (ImGui.Button(str + "##btn", new(ImGui.GetWindowWidth() / 3, default)))
        {
            window.SetSwitchSelected(rf);
        }
        if (disable)
            ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 2 / 3 - ImGui.GetStyle().ItemSpacing.X * 2);
        if (ImGui.InputInt("##" + str, ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            i = Math.Clamp(i, -1, 9999);
            window.CurrentScene.ChangeSwitch(i, rf, sco);
            rf = i;
            //ChangeHandler.ChangeSwitch(window.CurrentScene.History, sto, str, rf, i);
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
