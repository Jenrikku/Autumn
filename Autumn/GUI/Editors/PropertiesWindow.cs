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
    private ISceneObj? prevObj = null;
    private StageObj multiselector = new();
    DragFloat3 PosDrag = new(window);
    DragFloat3 RotDrag = new(window);
    LinkedDragFloat3 ScaleDrag = new(window);
    ImGuiWidgets.InputComboBox namebox = new();
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.AutoHideTabBar | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; //ImGuiWidgets.NO_TAB_BAR };

    private List<StageCamera> cameraslinks;
    private string[] cameraStrings;

    public bool updateCameras = false;
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
            if (sceneObj != prevObj)
            {
                if (prevObj != null)
                {
                    PosDrag.Finish(ref prevObj.StageObj.Translation);
                    RotDrag.Finish(ref prevObj.StageObj.Rotation);
                    ScaleDrag.Finish(ref prevObj.StageObj.Scale);
                    prevObj.UpdateTransform();
                }
                updateCameras = true;
                prevObj = sceneObj;
            }

            if (updateCameras)
            {
                var cams = window.CurrentScene.Stage.CameraParams.Cameras;
                if (sceneObj.StageObj.Name == "EntranceCameraObj") cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Entrance).ToList();
                else if (sceneObj.StageObj.Type == StageObjType.CameraArea) cameraslinks =  cams.Where(x => x.Category == StageCamera.CameraCategory.Map).ToList();
                else if (sceneObj.StageObj.Type == StageObjType.DemoScene) cameraslinks =  cams.Where(x => x.Category == StageCamera.CameraCategory.Event).ToList();
                else cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Object).ToList();

                cameraStrings = new string[cameraslinks.Count() + 1];
                cameraStrings[0] = "No camera selected";
                updateCameras = false;
            }


            string oldName = stageObj.Name;
            ImGui.GetIO().ConfigDragClickToInputText = true; //TODO - MOVE TO EDITOR STARTUP SETUP
            ImGui.SetWindowFontScale(1.20f);

            // Fake dock
            string oldClassCCNT = GetClassFromCCNT(oldName);
            bool usedb = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(oldClassCCNT);
            bool usedbName = usedb && ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Name != null;
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4);
            ImGui.Text(stageObj.Type.ToString() + ": " + (usedbName ? ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Name : oldName));
            ImGui.SetWindowFontScale(1.0f);
            bool useDesc = usedb && ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Description != null;
            if (useDesc) ImGui.SetItemTooltip(ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Description);

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
            //ImGui.SetNextItemWidth(prevW + 200);
            if (ImGui.BeginChild("PropertiesReal", new(ImGui.GetContentRegionAvail().X, default)))
            {
                //ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.Border));
                if (ImGui.CollapsingHeader("General Info", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("gen", default, ImGuiChildFlags.AutoResizeY );

                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                    //ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3.1f);
                    ImGui.PushItemWidth(prevW - PROP_WIDTH);
                    //namebox.Use("Name", ref stageObj.Name, window.ContextHandler.FSHandler.ReadCreatorClassNameTable().Keys.ToList());
                    //InputText("Name", ref stageObj.Name, 128, ref stageObj);

                    string namae =  stageObj.Name;

                    ImGui.Text("Name:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidth("Name")- ((stageObj is not RailObj) ? 28 : 0));
                    if (ImGui.InputTextWithHint("##namest", "Object Name", ref namae, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        ChangeHandler.ChangeFieldValue(window.CurrentScene?.History!, stageObj, "Name", stageObj.Name, namae);
                    }
                    if (stageObj is not RailObj)
                    {
                        string newClassCCNT = GetClassFromCCNT(stageObj.Name);

                        ImGui.SameLine(default, style.ItemInnerSpacing.X);

                        bool hascl = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(newClassCCNT);

                        if (!hascl) ImGui.BeginDisabled();

                        if (ImGui.Button(IconUtils.BOOK))
                        {
                            window.OpenDbEntryDialog(ClassDatabaseWrapper.DatabaseEntries[newClassCCNT]);
                        }
                        if (!hascl) ImGui.EndDisabled();
                        if (window.ContextHandler.Settings.UseClassNames)
                        {
                            string hint = string.Empty;

                            if (string.IsNullOrEmpty(stageObj.ClassName))
                                hint = newClassCCNT;

                            stageObj.ClassName ??= ""; // So that input text works well.

                            InputText("ClassName", ref stageObj.ClassName, 128, ref stageObj, hint);
                        }
                        else
                        {
                            ImGui.SetItemTooltip(newClassCCNT);
                            ImGui.BeginDisabled();
                            var s = "";
                            InputText("ClassName", ref s, 128, ref stageObj, newClassCCNT);
                            ImGui.EndDisabled();
                        }
                        //ImGui.Text("File type: " + stageObj.FileType);
                        //ImGui.Text("Object type: "+ stageObj.Type);

                    }
                    InputText("Layer", ref stageObj.Layer, 30, ref stageObj);
                    if (stageObj.Type != StageObjType.Start)
                    {
                        InputInt("ViewId", ref stageObj.ViewId, 1, ref stageObj);
                        ImGui.Text("Camera Id:"); ImGui.SameLine();
                        for (int cs = 1; cs < cameraStrings.Length; cs++)
                        {
                            cameraStrings[cs] = cameraslinks[cs - 1].CameraName();
                        }
                        var csd = cameraslinks.FirstOrDefault(x => x.UserGroupId == stageObj.CameraId);
                        int rff = 0;
                        if (csd != null) rff = cameraslinks.IndexOf(csd)+1;
                        else if (stageObj.CameraId != -1) stageObj.CameraId = -1; // Make sure cameras return to -1 if the camera is removed, since the combobox shows that, it should be coherent with it
                        int orff = rff;
                        //ImGuiWidgets.PrePropertyWidthName("Camera Id", 30, 20);
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidth("Camera Id:") - ImGui.CalcTextSize(IconUtils.PENCIL).X * 1.65f * window.ScalingFactor);
                        ImGui.Combo("##CAMERA SELECT", ref rff, cameraStrings, cameraStrings.Length);
                        if (rff != orff)
                        {
                            if (rff == 0) stageObj.CameraId = -1;
                            else stageObj.CameraId = cameraslinks[rff-1].UserGroupId;
                        }

                        ImGui.SameLine(default, style.ItemSpacing.X / 2);
                        if (ImGui.Button(rff == 0 ? IconUtils.PLUS : IconUtils.PENCIL)) // Edit the camera -> open the camera window and select it. Add if no camera selected
                        {
                            
                            ImGui.SetWindowFocus("Cameras");
                            if (rff == 0)
                            {
                                StageCamera.CameraCategory camType = CameraParams.GetObjectCategory(sceneObj.StageObj);
                                StageCamera newCam = new() {Category = camType};
                                window.CurrentScene.Stage.CameraParams.AddCamera(newCam);
                                window.SetCameraSelected(window.CurrentScene.Stage.CameraParams.Cameras.Count-1);
                                window.UpdateCameraList();
                                sceneObj.StageObj.CameraId = newCam.UserGroupId;
                            }
                            else
                            {
                                StageCamera.CameraCategory camType = CameraParams.GetObjectCategory(sceneObj.StageObj);
                                var cm = window.CurrentScene.Stage.CameraParams.GetCamera(sceneObj.StageObj.CameraId, camType);
                                window.SetCameraSelected(window.CurrentScene.Stage.CameraParams.Cameras.IndexOf(cm!));
                            }
                        }

                        InputInt("ClippingGroupId", ref stageObj.ClippingGroupId, 1, ref stageObj);
                    }
                    if (stageObj.Type == StageObjType.Area || stageObj.Type == StageObjType.CameraArea)
                    {
                        if (!stageObj.Properties.ContainsKey("Priority"))
                        {
                            ImGui.Text("Priority :");
                            ImGui.SameLine();
                            ImGuiWidgets.SetPropertyWidth("Priority");
                            if (ImGui.Button("Add property"))
                            {
                                stageObj.Properties["Priority"] = -1;
                            }
                        }
                        else{
                            int shp = (int)stageObj.Properties["Priority"]!;
                            InputIntProperties("Priority", ref shp, 1, ref stageObj);
                        }

                        if (!stageObj.Properties.ContainsKey("ShapeModelNo"))
                        {
                            ImGui.Text("ShapeModelNo:");
                            ImGui.SameLine();
                            ImGuiWidgets.SetPropertyWidth("ShapeModelNo");
                            if (ImGui.Button("No shape property"))
                            {
                                stageObj.Properties["ShapeModelNo"] = 0;
                            }
                        }
                        else{
                            int shp = (int)stageObj.Properties["ShapeModelNo"]!;
                            InputIntProperties("ShapeModelNo", ref shp, 1, ref stageObj);
                        }
                    }
                    ImGui.PopItemWidth();
                    //ImGui.PopStyleVar();
                    ImGui.EndChild();
                }

                if (ImGui.CollapsingHeader("Object Transform", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("trl", default, ImGuiChildFlags.AutoResizeY );
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
                        ImGui.BeginChild("arg", default, ImGuiChildFlags.AutoResizeY );
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
                                            string aName = string.IsNullOrEmpty(argEntry.Name) ? "Unknown" : argEntry.Name;
                                            ImGui.Text(aName + ":");
                                            if (argEntry.Type == "enum")
                                            {
                                                var rf = argEntry.Values.Keys.ToList().IndexOf(intBuf);
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(aName);
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
                                                    //ImGui.SetItemTooltip(argEntry.Values[argEntry.Values.Keys.ElementAt(rf)]);
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
                                                ImGuiWidgets.SetPropertyWidth(aName);
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
                                                ImGuiWidgets.SetPropertyWidth(aName);
                                                ImGui.InputInt("##" + argEntry.Name + "i", ref rf, 1, default);
                                                rf = int.Clamp(rf, argEntry.Min ?? -99999, argEntry.Max ?? 99999);
                                                if (intBuf != rf)
                                                {
                                                    stageObj.Properties[name] = rf;
                                                }
                                            }
                                            ImGui.SetItemTooltip(argEntry.Description != null && !string.IsNullOrEmpty(argEntry.Description) ? argEntry.Description : "No description");
                                        }
                                        else
                                        {
                                            InputIntProperties(name, ref intBuf, 1, ref stageObj);
                                            ImGui.SetItemTooltip("No description");
                                        }
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
                        ImGui.BeginChild("swc", default, ImGuiChildFlags.AutoResizeY );
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
                            if (ImGui.BeginChild("pnt", default, ImGuiChildFlags.AutoResizeY ))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                                ImGui.Text("Parent:");
                                ImGui.SameLine();
                                string pName = stageObj.Parent is not null ? stageObj.Parent.Name : "No parent";
                                if (stageObj.Parent == null)
                                    ImGui.BeginDisabled();
                                if (ImGui.Button(pName, new(ImGuiWidgets.SetPropertyWidth("Parent") - ImGui.CalcTextSize(IconUtils.UNLINK).X * 1.65f * window.ScalingFactor, default)))
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
                                if (ImGui.Button(IconUtils.UNLINK + "##" + pName))
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
                                    if (ImGui.BeginTable("childrenTable", 4,
                                        ImGuiTableFlags.RowBg
                                        | ImGuiTableFlags.BordersOuter
                                        | ImGuiTableFlags.BordersV
                                        | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - style.WindowPadding.X, autoResize ? default : 150 * window.ScalingFactor)))
                                    {
                                        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                                        ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.None);
                                        ImGui.TableSetupColumn("Unlink", ImGuiTableColumnFlags.None);
                                        ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None);
                                        ImGui.TableHeadersRow();
                                        if (autoResize) ImGui.SetScrollY(0);
                                        int cidx = 0;
                                        StageObj? remch = null;
                                        foreach (StageObj ch in stageObj.Children) // This keeps child order!
                                        {
                                            ImGui.TableNextRow();

                                            ImGui.TableSetColumnIndex(2);

                                            ImGui.PushID("SceneChildSelectable" + cidx);
                                            if (ImGui.Selectable(ch.Name, false))
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

                                            ImGui.TableSetColumnIndex(3);

                                            ImGui.Text(ch.Type.ToString());
                                            
                                            ImGui.PushStyleColor(ImGuiCol.Button, 0);
                                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                                            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
                                            ImGui.TableSetColumnIndex(0);
                                            ImGui.PushID("SceneChildView" + cidx);
                                            if (ImGui.Button(IconUtils.MAG_GLASS,new(-1, 25)))
                                            {
                                                var child = window.CurrentScene.GetSceneObjFromStageObj(ch);
                                                AxisAlignedBoundingBox aabb = child.AABB * ch.Scale;
                                                window.CurrentScene!.Camera.LookFrom(ch.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                                            }

                                            ImGui.TableSetColumnIndex(1);
                                            ImGui.PushID("SceneChildUnlink" + cidx);
                                            if (ImGui.Button(IconUtils.UNLINK, new(-1, 25)))
                                            {
                                                remch = ch;
                                            }
                                            ImGui.PopStyleColor(3);

                                            cidx++;
                                        }
                                        if (remch != null) window.CurrentScene.Stage.GetStageFile(StageFileType.Map).UnlinkChild(remch);

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
                        ImGui.BeginChild("rl", default, ImGuiChildFlags.AutoResizeY );
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                        ImGui.Text("Rail: ");
                        ImGui.SameLine();

                        var rails = window.CurrentScene.EnumerateSceneObjs().Where(x => x is RailSceneObj);
                        string[] railStrings = new string[rails.Count() + 1];
                        int bbbb = 1;
                        railStrings[0] = "No Rail selected";
                        foreach (RailSceneObj rail in rails)
                        {
                            railStrings[bbbb] = rail.RailObj.Name;
                            bbbb += 1;
                        }
                        int rfrail = 0;
                        if (stageObj.Rail is not null)
                        {
                            rfrail = rails.ToList().IndexOf(rails.First(x => x.StageObj.Name == stageObj.Rail.Name)) + 1;
                        }
                        int rfr2 = rfrail;
                        ImGuiWidgets.SetPropertyWidth("Rail");
                        ImGui.Combo("##Railselector", ref rfr2, railStrings, rails.Count() + 1);
                        if (rfr2 != rfrail)
                        {
                            if (rfr2 > 0)
                            {
                                stageObj.Rail = (RailObj)rails.ElementAt(rfr2 - 1).StageObj;
                            }
                            else stageObj.Rail = null;
                        }

                        ImGui.EndChild();
                    }
                }

                if (ImGui.CollapsingHeader("Extra Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("prp", default, ImGuiChildFlags.AutoResizeY );
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                    ImGui.PushItemWidth(prevW - PROP_WIDTH);
                    
                    foreach (var (name, property) in stageObj.Properties)
                    {
                        if (property is null)
                        {
                            ImGui.TextDisabled(name);
                            continue;
                        }
                        if (name.Contains("Arg") || name == "Priority" || name == "ShapeModelNo") continue;

                        if (ImGui.Button(IconUtils.TRASH + "##rmp" + name))
                        {
                            stageObj.Properties.Remove(name); 
                            continue;
                        } 
                        ImGui.SameLine(default, style.ItemInnerSpacing.X);
                        if (ImGui.Button(IconUtils.PENCIL + "##edit" + name)) 
                        {
                            window.SetupExtraPropsDialog(stageObj, name);
                        }
                        ImGui.SameLine();
                        ExtraPropName(name);
                        switch (property)
                        {
                            case object p when p is int:
                                int intBuf = (int)(p ?? -1);
                                int i = intBuf;
                                if (ImGui.InputInt("##" + name + "i", ref i, 1, default, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    ChangeHandler.ChangeDictionaryValue(window.CurrentScene?.History!, stageObj.Properties, name, intBuf, i);
                                }
                                break;
                            case object p when p is float:
                                float flBuf = (float)(p ?? -1);
                                float f = flBuf;
                                if (ImGui.InputFloat("##" + name + "i", ref f, 1, default, default, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, stageObj.Properties, name, flBuf, f);
                                }
                                break;
                            case object p when p is string:
                                string strBuf = (string)(p ?? string.Empty);
                                string s = strBuf;
                                if (ImGui.InputText("##" + name + "i", ref s, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, stageObj.Properties, name, strBuf, s);
                                }
                                break;
                            case object p when p is bool:
                                bool bl = (bool)(p ?? false);
                                bool b = bl;
                                if (ImGui.Checkbox("##" + name + "i", ref b))
                                {
                                    ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, stageObj.Properties, name, bl, b);
                                }
                                break;

                            default:
                                throw new NotImplementedException(
                                    "The property type " + property?.GetType().FullName
                                        ?? "null" + " is not supported."
                                );
                        }
                    }

                    if (ImGui.Button("Add Property", new(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X, default)))
                    {
                        window.SetupExtraPropsDialogNew(stageObj);
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
            //InputInt("CameraId", ref multiselector.CameraId, 1, ref multiselector);
            ImGui.Text("CameraId"); ImGui.SameLine();
            var cameraslinks = window.CurrentScene.Stage.CameraParams.Cameras.Where(x => x.Category == StageCamera.CameraCategory.Object).ToList();
            string[] cameraStrings = new string[cameraslinks.Count() + 1];
            cameraStrings[0] = "No camera selected";
            for (int cs = 1; cs < cameraStrings.Length; cs++)
            {
                cameraStrings[cs] = cameraslinks[cs - 1].Category.ToString();
                cameraStrings[cs] += "Camera";
                cameraStrings[cs] += cameraslinks[cs - 1].UserGroupId;
            }
            int rff = 0;
            ImGui.Combo("##CAMERA SELECT", ref rff, cameraStrings, cameraStrings.Length);
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

            if (ImGui.IsItemActive() && ImGui.IsItemFocused() && !ImGui.IsMouseDragging(ImGuiMouseButton.Left) && isFinished)
            {
                isEditing = true;
                isFinished = false;
            }
            else if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && ImGui.IsItemActive() && ImGui.IsItemFocused() ) 
            {
                isDragging = true;
                isFinished = false;
            }
            if (isDragging)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    Complete(ref rf);
                    return true;
                }
            }
            if (isEditing)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Tab) || ImGui.IsKeyPressed(ImGuiKey.Enter))
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
        public void Finish(ref float rf) => Reset(ref rf);

        void Complete(ref float rf)
        {
            isEditing = false;
            isDragging = false;
            isFinished = true;
            rf = reference;
        }
        void Reset(ref float rf)
        {
            isEditing = false;
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
                    itemWidth = ImGui.GetWindowWidth() * 3 / 4 - ImGui.GetStyle().ItemSpacing.X / 2 - 24 * _window.ScalingFactor;
                }
                else
                {
                    itemWidth = ImGui.GetWindowWidth() - stringWidth - ImGui.GetStyle().ItemSpacing.X * 2 - 24 * _window.ScalingFactor;
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
            if (ImGui.BeginChild(str + "XTest", new(20 * _window.ScalingFactor, 20 * _window.ScalingFactor + style.ItemSpacing.Y)))
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
            if (ImGui.BeginChild(str + "YTest", new(20 * _window.ScalingFactor, 20 * _window.ScalingFactor + style.ItemSpacing.Y)))
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
            if (ImGui.BeginChild(str + "ZTest", new(20 * _window.ScalingFactor, 20 * _window.ScalingFactor + style.ItemSpacing.Y)))
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

        public void Finish(ref Vector3 rf)
        {
            DragFloatX.Finish(ref rf.X);
            DragFloatY.Finish(ref rf.Y);
            DragFloatZ.Finish(ref rf.Z);
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
            if (ImGui.Button(isLinked ? IconUtils.LINK : IconUtils.UNLINK))
            {
                isLinked = !isLinked;
            }
            return ret;
        }

        public void Finish(ref Vector3 rf)
        {
            ScaleDrag3.Finish(ref rf);
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
    private void ExtraPropName(string name)
    {
        ImGui.Text(name+":");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(name + IconUtils.TRASH + IconUtils.TRASH + IconUtils.TRASH, 1, 2));
    }

    private bool InputSwitch(string str, ref int rf, int step, ref ISceneObj sco)
    {
        int i = rf;

        string tt = "";
        string db = GetClassFromCCNT(sco.StageObj.Name);
        if (ClassDatabaseWrapper.DatabaseEntries.ContainsKey(db))
        {
            var c = ClassDatabaseWrapper.DatabaseEntries[db];
            if (c.Switches != null && c.Switches.ContainsKey($"Switch{str}") && c.Switches[$"Switch{str}"] != null)
            {
                tt = c.Switches[$"Switch{str}"]?.Type + ": " + c.Switches[$"Switch{str}"]?.Description;
            }
        }

        bool disable = rf < 0;
        if (disable)
            ImGui.BeginDisabled();
        if (ImGui.Button(str + "##btn", new(ImGui.GetWindowWidth() / 3, default)))
        {
            window.SetSwitchSelected(rf);
        }
        if (disable)
            ImGui.EndDisabled();

        if (tt != "")
            ImGui.SetItemTooltip(tt);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 2 / 3 - ImGui.GetStyle().ItemSpacing.X * 2);
        if (ImGui.InputInt("##" + str, ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            i = Math.Clamp(i, -1, 9999);
            window.CurrentScene.ChangeSwitch(i, rf, sco);
            rf = i;
            //ChangeHandler.ChangeSwitch(window.CurrentScene.History, sto, str, rf, i);
        }
        if (tt != "")
            ImGui.SetItemTooltip(tt);

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
