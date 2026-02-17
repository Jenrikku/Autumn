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

    private List<StageCamera> cameraslinks = new();
    private string[] cameraStrings = [];

    public bool updateCameras = false;
    public bool updateRailOwners = false;

    public List<IStageSceneObj> railSceneOwners = new();
    public List<StageCamera> railCameraOwners = new();

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

        var scn = window.CurrentScene;

        IEnumerable<ISceneObj> selectedObjects = scn.SelectedObjects;
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
        var style = ImGui.GetStyle();
        float prevW = ImGui.GetWindowWidth();

        ISceneObj sceneObj = selectedObjects.First();

        if (sceneObj != prevObj)
        {
            if (prevObj != null)
            {
                if (prevObj is IStageSceneObj prevStageSceneObj)
                {
                    PosDrag.Finish(ref prevStageSceneObj.StageObj.Translation);
                    RotDrag.Finish(ref prevStageSceneObj.StageObj.Rotation);
                    ScaleDrag.Finish(ref prevStageSceneObj.StageObj.Scale);
                }
                else if (prevObj is RailPointSceneObj rps)
                {
                    PosDrag.Finish(ref rps.RailPoint.Point0Trans);
                }
                else if (prevObj is RailHandleSceneObj rph)
                {
                    PosDrag.Finish(ref rph.Offset);
                }

                prevObj.UpdateTransform();
            }

            updateCameras = true;
            updateRailOwners = true;
            prevObj = sceneObj;
        }

        //ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.8f, 0.5f));
        // Only one object selected and contains a StageObj:
        if (selectedCount == 1)
        {
            if (sceneObj is IStageSceneObj stageSceneObj)
            {
            StageObj stageObj = stageSceneObj.StageObj;

            if (updateCameras)
            {
                var cams = scn.Stage.CameraParams.Cameras;
                if (stageObj.Name == "EntranceCameraObj") cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Entrance).ToList();
                else if (stageObj.Type == StageObjType.CameraArea) cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Map).ToList();
                else if (stageObj.Type == StageObjType.DemoScene) cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Event).ToList();
                else cameraslinks = cams.Where(x => x.Category == StageCamera.CameraCategory.Object).ToList();

                cameraStrings = new string[cameraslinks.Count + 1];
                cameraStrings[0] = "No camera selected";
                updateCameras = false;
            }

            string oldName = stageObj.Name;
            ImGui.GetIO().ConfigDragClickToInputText = true; //TODO - MOVE TO EDITOR STARTUP SETUP

            // Fake dock
            string oldClassCCNT = GetClassFromCCNT(oldName);
            bool usedb = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(oldClassCCNT);
            bool usedbName = usedb && ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Name != null;
            bool useDesc = usedb && ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Description != null;
            float ypos = SetTitle(stageObj.Type.ToString() + ": " + (usedbName ? ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Name : oldName), useDesc ? ClassDatabaseWrapper.DatabaseEntries[oldClassCCNT].Description : null);

            bool scrollToChild = false;
            if (stageObj.Parent is not null)
            {
                ImGui.SetCursorPosY(ypos);
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X-20);
                if (ImGui.Button("P", new(20, default)))
                {
                    window.ContextHandler.ActionHandler.ExecuteAction(CommandID.GotoRelative, window);
                }
                ImGui.SetItemTooltip("Jump to parent");
            }
            else if (stageObj.Children != null && stageObj.Children.Count > 0)
            {
                ImGui.SetCursorPosY(ypos);
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X-20);
                if (ImGui.Button("C", new(20, default)))
                {
                    if (stageObj.Children.Count == 1)
                    {
                        window.ContextHandler.ActionHandler.ExecuteAction(CommandID.GotoRelative, window);
                    }
                    else
                        scrollToChild = true;
                }
                ImGui.SetItemTooltip($"Jump to {(stageObj.Children.Count == 1 ? "child" : "children")}");
            }
            //ImGui.SetNextItemWidth(prevW + 200);
            if (ImGui.BeginChild("PropertiesReal", new(ImGui.GetContentRegionAvail().X, default)))
            {
                if (ImGui.CollapsingHeader("General Info", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("gen", default, ImGuiChildFlags.AutoResizeY);

                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                    //ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3.1f);
                    ImGui.PushItemWidth(prevW - PROP_WIDTH);
                    //namebox.Use("Name", ref stageObj.Name, window.ContextHandler.FSHandler.ReadCreatorClassNameTable().Keys.ToList());
                    //InputText("Name", ref stageObj.Name, 128, ref stageObj);

                    string name = stageObj.Name;

                    ImGui.Text("Name:");
                    ImGui.SameLine();
                    var wdth = ImGuiWidgets.SetPropertyWidth("Name") - ((stageObj is not RailObj) ? 28 : 0);
                    ImGui.SetNextItemWidth(wdth);
                    if (ImGui.InputTextWithHint("##namest", "Object Name", ref name, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        ChangeHandler.ChangeActorName(window, scn.History!, stageSceneObj, stageObj.Name, name);
                    }

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
                        if (csd != null) rff = cameraslinks.IndexOf(csd) + 1;
                        else if (stageObj.CameraId != -1) stageObj.CameraId = -1; // Make sure cameras return to -1 if the camera is removed, since the combobox shows that, it should be coherent with it
                        int orff = rff;
                        //ImGuiWidgets.PrePropertyWidthName("Camera Id", 30, 20);
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidth("Camera Id:") - ImGui.CalcTextSize(rff == 0 ? IconUtils.PLUS : IconUtils.PENCIL).X - 12);
                        ImGui.Combo("##CAMERA SELECT", ref rff, cameraStrings, cameraStrings.Length);
                        if (rff != orff)
                        {
                            if (rff == 0) stageObj.CameraId = -1;
                            else stageObj.CameraId = cameraslinks[rff - 1].UserGroupId;
                        }

                        ImGui.SameLine(default, style.ItemSpacing.X / 2);
                        if (ImGui.Button(rff == 0 ? IconUtils.PLUS : IconUtils.PENCIL)) // Edit the camera -> open the camera window and select it. Add if no camera selected
                        {

                            ImGui.SetWindowFocus("Cameras");
                            if (rff == 0)
                            {
                                StageCamera.CameraCategory camType = CameraParams.GetObjectCategory(stageObj);
                                StageCamera newCam = new() { Category = camType };
                                scn!.Stage.CameraParams.AddCamera(newCam);
                                window.SetCameraSelected(scn.Stage.CameraParams.Cameras.Count - 1);
                                window.UpdateCameraList();
                                stageObj.CameraId = newCam.UserGroupId;
                            }
                            else
                            {
                                StageCamera.CameraCategory camType = CameraParams.GetObjectCategory(stageObj);
                                var cm = scn!.Stage.CameraParams.GetCamera(stageObj.CameraId, camType);
                                window.SetCameraSelected(scn.Stage.CameraParams.Cameras.IndexOf(cm!));
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
                        else
                        {
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
                        else
                        {
                            int shp = (int)stageObj.Properties["ShapeModelNo"]!;
                            InputIntProperties("ShapeModelNo", ref shp, 1, ref stageObj);
                        }
                    }
                    ImGui.PopItemWidth();
                    ImGui.EndChild();
                }

                if (ImGui.CollapsingHeader("Object Transform", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("trl", default, ImGuiChildFlags.AutoResizeY);
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
                        ImGui.BeginChild("arg", default, ImGuiChildFlags.AutoResizeY);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
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
                                                    ImGui.InputInt("##" + name, ref rf, 1, default);
                                                    if (intBuf != rf)
                                                    {
                                                        ChangeHandler.ChangeDictionaryValue(scn.History, stageObj.Properties, name, intBuf, rf);
                                                    }
                                                }
                                                else
                                                {
                                                    ImGui.Combo("##" + name + "c",
                                                    ref rf,
                                                    argEntry.Values.Values.ToArray(),
                                                    argEntry.Values.Count
                                                    );
                                                    //ImGui.SetItemTooltip(argEntry.Values[argEntry.Values.Keys.ElementAt(rf)]);
                                                    if (intBuf != argEntry.Values.Keys.ElementAt(rf))
                                                    {
                                                        ChangeHandler.ChangeDictionaryValue(scn.History, stageObj.Properties, name, intBuf, argEntry.Values.Keys.ElementAt(rf));
                                                    }
                                                }
                                            }
                                            else if (argEntry.Type == "bool")
                                            {
                                                var rf = intBuf != -1;
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(aName);
                                                ImGui.Checkbox("##" + name + "cb", ref rf);
                                                if ((intBuf != -1) != rf)
                                                {
                                                    ChangeHandler.ChangeDictionaryValue(scn.History, stageObj.Properties, name, intBuf, rf ? 1 : -1);
                                                }
                                            }
                                            else // if (argEntry.Type is null || argEntry.Type == "int")
                                            {
                                                var rf = intBuf;
                                                ImGui.SameLine();
                                                ImGuiWidgets.SetPropertyWidth(aName);
                                                ImGui.InputInt("##" + name + "i", ref rf, 1, default);
                                                rf = int.Clamp(rf, argEntry.Min ?? -99999, argEntry.Max ?? 99999);
                                                if (intBuf != rf)
                                                {
                                                    ChangeHandler.ChangeDictionaryValue(scn.History, stageObj.Properties, name, intBuf, rf);
                                                    //stageObj.Properties[name] = rf;
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
                        ImGui.BeginChild("swc", default, ImGuiChildFlags.AutoResizeY);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                        ImGui.PushItemWidth(prevW - PROP_WIDTH);
                        InputSwitch("A", ref stageObj.SwitchA, 1, ref stageSceneObj);
                        InputSwitch("B", ref stageObj.SwitchB, 1, ref stageSceneObj);
                        InputSwitch("Appear", ref stageObj.SwitchAppear, 1, ref stageSceneObj);
                        InputSwitch("DeadOn", ref stageObj.SwitchDeadOn, 1, ref stageSceneObj);
                        InputSwitch("Kill", ref stageObj.SwitchKill, 1, ref stageSceneObj);
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
                            if (ImGui.BeginChild("pnt", default, ImGuiChildFlags.AutoResizeY))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                                ImGui.Text("Parent:");
                                ImGui.SameLine();
                                string pName = stageObj.Parent is not null ? stageObj.Parent.Name : "No parent";
                                if (stageObj.Parent == null)
                                    ImGui.BeginDisabled();
                                if (ImGui.Button(pName, new(ImGuiWidgets.SetPropertyWidth("Parent") - ImGui.CalcTextSize(IconUtils.UNLINK).X * 1.65f * window.ScalingFactor, default)))
                                {
                                    var p = scn!.GetSceneObjFromStageObj(stageObj.Parent!);
                                    ChangeHandler.ToggleObjectSelection( window, scn.History, p.PickingId,
                                        !(window.Keyboard?.IsCtrlPressed() ?? false));
                                    window.CameraToObject(p);
                                }

                                ImGui.SameLine(default, style.ItemSpacing.X / 2);
                                if (ImGui.Button(IconUtils.UNLINK + "##" + pName))
                                {
                                    ChangeHandler.ChangeUnlinkChild(window, window.CurrentScene.History, stageObj);
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
                                        | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - style.WindowPadding.X, 22 + (autoResize ? 34 * stageObj.Children.Count : 34 * 6) * window.ScalingFactor)))
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
                                            if (ImGui.Selectable(ch.Name, false, ImGuiSelectableFlags.None, new(ImGui.GetColumnWidth(), 30)))
                                            {
                                                var child = scn.GetSceneObjFromStageObj(ch);
                                                ChangeHandler.ToggleObjectSelection(window, scn.History,
                                                    child.PickingId, !window.Keyboard?.IsCtrlPressed() ?? true);
                                                window.CameraToObject(child);
                                            }

                                            ImGui.TableSetColumnIndex(3);

                                            ImGui.Text(ch.Type.ToString());

                                            ImGui.PushStyleColor(ImGuiCol.Button, 0);
                                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                                            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
                                            ImGui.TableSetColumnIndex(0);
                                            ImGui.PushID("SceneChildView" + cidx);
                                            if (ImGuiWidgets.HoverButton(IconUtils.MAG_GLASS, new(ImGui.GetColumnWidth(), 30)))
                                            {
                                                var child = scn.GetSceneObjFromStageObj(ch);
                                                window.CameraToObject(child);
                                            }

                                            ImGui.TableSetColumnIndex(1);
                                            ImGui.PushID("SceneChildUnlink" + cidx);
                                            if (ImGuiWidgets.HoverButton(IconUtils.UNLINK, new(ImGui.GetColumnWidth(), 30)))
                                            {
                                                remch = ch;
                                            }
                                            ImGui.PopStyleColor(3);

                                            cidx++;
                                        }
                                        if (remch != null) 
                                            ChangeHandler.ChangeUnlinkChild(window, window.CurrentScene.History, remch);

                                        ImGui.EndTable();
                                        ImGui.Spacing();
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
                        ImGui.BeginChild("rl", default, ImGuiChildFlags.AutoResizeY);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                        ImGui.Text("Rail: ");
                        ImGui.SameLine();

                        var rails = scn!.EnumerateRailSceneObjs();
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
                            var rals = rails.FirstOrDefault(x => x.RailObj.Name == stageObj.Rail.Name); // In case the rail gets deleted
                            if (rals != null)
                                rfrail = rails.ToList().IndexOf(rals) + 1;
                        }
                        int rfr2 = rfrail;
                        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen("Rail") - ImGui.CalcTextSize(IconUtils.PENCIL).X * 1.65f * window.ScalingFactor + 7);

                        ImGui.Combo("##Railselector", ref rfr2, railStrings, rails.Count() + 1);
                        if (rfr2 != rfrail)
                        {
                            if (rfr2 > 0)
                            {
                                stageObj.Rail = rails.ElementAt(rfr2 - 1).RailObj;
                            }
                            else stageObj.Rail = null;
                        }
                        ImGui.SameLine(0, style.ItemInnerSpacing.X);
                        if (rfr2 == 0) ImGui.BeginDisabled();
                        if (ImGui.Button(IconUtils.PENCIL +"##railaddedit"))
                        {
                            if (rfr2 == 0)
                            {
                                window.OpenAddRailDialog();
                            }
                            else
                            {
                                var child = scn.GetRailSceneObj(stageObj.Rail!);
                                ChangeHandler.ToggleObjectSelection( window, scn.History, child!.PickingId,
                                    !window.Keyboard?.IsCtrlPressed() ?? true);
                                window.CameraToObject(child);
                            }
                        }
                        if (rfr2 == 0) ImGui.EndDisabled();

                        ImGui.EndChild();
                    }
                }

                if (ImGui.CollapsingHeader("Extra Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("prp", default, ImGuiChildFlags.AutoResizeY);
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
                                    ChangeHandler.ChangeDictionaryValue(scn?.History!, stageObj.Properties, name, intBuf, i);
                                }
                                break;
                            case object p when p is float:
                                float flBuf = (float)(p ?? -1);
                                float f = flBuf;
                                if (ImGui.InputFloat("##" + name + "i", ref f, 1, default, default, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    ChangeHandler.ChangeDictionaryValue(scn!.History, stageObj.Properties, name, flBuf, f);
                                }
                                break;
                            case object p when p is string:
                                string strBuf = (string)(p ?? string.Empty);
                                string s = strBuf;
                                if (ImGui.InputText("##" + name + "i", ref s, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    ChangeHandler.ChangeDictionaryValue(scn!.History, stageObj.Properties, name, strBuf, s);
                                }
                                break;
                            case object p when p is bool:
                                bool bl = (bool)(p ?? false);
                                bool b = bl;
                                if (ImGui.Checkbox("##" + name + "i", ref b))
                                {
                                    ChangeHandler.ChangeDictionaryValue(scn!.History, stageObj.Properties, name, bl, b);
                                }
                                break;

                            default:
                                throw new NotImplementedException(
                                    "The property type " + property?.GetType().FullName
                                        ?? "null" + " is not supported."
                                );
                        }
                    }

                    if (ImGui.Button("Add Property", new(ImGui.GetContentRegionAvail().X - style.ItemSpacing.X, default)))
                    {
                        window.SetupExtraPropsDialogNew(stageObj);
                    }

                    ImGui.PopItemWidth();
                    ImGui.EndChild();
                }

                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
            }
            else if (sceneObj is RailSceneObj || sceneObj is RailPointSceneObj || sceneObj is RailHandleSceneObj)
            {
                RailSceneObj railSceneObj;
                string t;
                
                if (sceneObj is RailSceneObj)
                {
                    railSceneObj = (sceneObj as RailSceneObj)!;
                    t = $"Rail: {railSceneObj.RailObj.Name}";
                }
                else if (sceneObj is RailPointSceneObj)
                {
                    railSceneObj = (sceneObj as  RailPointSceneObj)!.ParentRail;
                    t = $"{railSceneObj.RailObj.Name} Point {railSceneObj.RailPoints.IndexOf((sceneObj as  RailPointSceneObj)!)}";
                }
                else
                {
                    railSceneObj = (sceneObj as RailHandleSceneObj)!.ParentPoint.ParentRail;
                    t = $"{railSceneObj.RailObj.Name} Point {railSceneObj.RailPoints.IndexOf((sceneObj as  RailHandleSceneObj)!.ParentPoint)}";
                }

                if (updateRailOwners)
                {
                    railSceneOwners = scn.EnumerateStageSceneObjs().Where(x => x.StageObj.Rail is not null && x.StageObj.Rail == railSceneObj.RailObj).ToList();
                    railCameraOwners = scn.Stage.CameraParams.Cameras.Where(x => x.CamProperties.Rail is not null && x.CamProperties.Rail == railSceneObj.RailObj).ToList();
                    updateRailOwners = false;
                }

                RailObj railObj = railSceneObj.RailObj;
                

                float ypos = SetTitle(t, null);

                if (sceneObj is not RailSceneObj)
                {
                    float xp = ImGui.GetContentRegionAvail().X;
                    ImGui.SetCursorPosY(ypos);
                    ImGui.SetCursorPosX(xp - ImGui.CalcTextSize("Rail").X);
                    if (ImGui.Button("Rail"))
                    {
                        ChangeHandler.ToggleObjectSelection(window, scn!.History, railSceneObj.PickingId, true);
                        window.CameraToObject(railSceneObj);
                    }
                    ImGui.SetItemTooltip("Jump to rail");
                    if (sceneObj is RailHandleSceneObj)
                    {
                        ImGui.SetCursorPosY(ypos);
                        ImGui.SetCursorPosX(xp - ImGui.CalcTextSize("Rail").X - ImGui.CalcTextSize("Point").X - style.ItemSpacing.X * 1.5f);
                        if (ImGui.Button("Point"))
                        {
                            ChangeHandler.ToggleObjectSelection(window, scn!.History, (sceneObj as RailHandleSceneObj)!.ParentPoint.PickingId, true);
                            window.CameraToObject((sceneObj as RailHandleSceneObj)!.ParentPoint);
                        }
                        ImGui.SetItemTooltip("Jump to point"); 
                    }
                }
                
                if (ImGui.BeginChild("PropertiesReal", new(ImGui.GetContentRegionAvail().X, default)))
                {
                    if (ImGui.CollapsingHeader("Rail Properties", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                        ImGui.BeginChild("rprop", default, ImGuiChildFlags.AutoResizeY);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                        ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);
                        string s = railObj.Name;
                        ImGuiWidgets.PrePropertyWidthName("Name");
                        if (ImGui.InputTextWithHint("##" + "Name" + "i", "hint", ref s, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            ChangeHandler.ChangeFieldValue(scn?.History!, railObj, "Name", railObj.Name, s);
                        }
                        ImGuiWidgets.PrePropertyWidthName("Layer");
                        ImGui.InputTextWithHint("##layername", "", ref railObj.Layer, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                        ImGuiWidgets.PrePropertyWidthName("Closed loop");
                        if (ImGui.Checkbox("##CLOSED", ref railObj.Closed))
                            railSceneObj.UpdateModelTmp();
                        int pointtyp = railObj.PointType == RailPointType.Bezier ? 1 : 0;
                        ImGuiWidgets.PrePropertyWidthName("Curve type");
                        if (ImGui.Combo("##Type", ref pointtyp, ["Linear", "Bezier"], 2))
                        {
                            railObj.PointType = pointtyp == 0 ? RailPointType.Linear : RailPointType.Bezier; 
                            railSceneObj.UpdateModelTmp();
                        }
                        ImGui.SetItemTooltip("Determines whether this rail will save with handles in the point positions or not. \r\nThis option is destructive after saving.");
                        // ImGui.Text($"Center X: {railSceneObj.Center.X}");
                        // ImGui.Text($"Center Y: {railSceneObj.Center.Y}");
                        // ImGui.Text($"Center Z: {railSceneObj.Center.Z}");
                        if (ImGui.CollapsingHeader("Rail Users", ImGuiTreeNodeFlags.DefaultOpen ))
                        {
                        if (ImGui.BeginTable("UserTable", 2,ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersH,
                        new(ImGui.GetWindowWidth()-2, 100)))
                        {
                            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                            ImGui.TableSetupColumn("User", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.3f);
                            ImGui.TableHeadersRow();

                            for (int b = 0; b < railSceneOwners.Count; b++)
                            {
                                ImGui.TableNextRow();

                                ImGui.TableSetColumnIndex(0);

                                ImGui.PushID("sceneslecteparentObject" + b);
                                if (ImGui.Selectable(railSceneOwners[b].StageObj.Name, false, ImGuiSelectableFlags.SpanAllColumns))
                                {
                                    ChangeHandler.ToggleObjectSelection(window, scn!.History, railSceneOwners[b].PickingId, true);

                                    window.CameraToObject(railSceneOwners[b]);
                                }
                                ImGui.TableSetColumnIndex(1);
                                ImGui.Text(railSceneOwners[b].StageObj.Type.ToString());
                            }
                            for (int b = 0; b < railCameraOwners.Count; b++)
                            {
                                ImGui.TableNextRow();

                                ImGui.TableSetColumnIndex(0);

                                ImGui.PushID("sceneslecteparentObject" + b);
                                if (ImGui.Selectable(railCameraOwners[b].CameraName(), false, ImGuiSelectableFlags.SpanAllColumns))
                                {
                                    window.SetCameraSelected(scn!.Stage.CameraParams.Cameras.IndexOf(railCameraOwners[b]!));
                                }
                                ImGui.TableSetColumnIndex(1);
                                ImGui.Text("Camera");
                            }

                            ImGui.EndTable();
                            ImGui.Spacing();
                        }
                        }
                        ImGui.PopItemWidth();
                        ImGui.EndChild();
                    }
                    if (sceneObj is RailSceneObj)
                    {
                        if (ImGui.CollapsingHeader($"Rail Points", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            ImGui.BeginChild("pointss", default, ImGuiChildFlags.AutoResizeY);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);

                            RailPointSceneObj? delay = null;
                            bool n = false;

                            bool autoResize = railObj.Points.Count < 8;
                            if (ImGui.BeginTable("pointTable", 4,
                                ImGuiTableFlags.RowBg
                                | ImGuiTableFlags.BordersOuter
                                | ImGuiTableFlags.BordersV
                                | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - style.WindowPadding.X, (autoResize ? -1 : 250 * window.ScalingFactor - 2))))
                            {
                                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                                ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.WidthStretch, 0.15f);
                                ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthStretch, 0.8f);
                                ImGui.TableSetupColumn("##UPs", ImGuiTableColumnFlags.WidthStretch, 0.1f);
                                ImGui.TableSetupColumn("##DOWNs", ImGuiTableColumnFlags.WidthStretch, 0.1f);
                                ImGui.TableHeadersRow();
                                if (autoResize) ImGui.SetScrollY(0);
                                int cidx = 0;
                                foreach (RailPoint ch in railObj.Points)
                                {
                                    ImGui.TableNextRow();

                                    ImGui.TableSetColumnIndex(1);

                                    ImGui.PushID("ScenePointSelectable" + cidx);
                                    if (ImGui.Selectable(cidx.ToString(), false, ImGuiSelectableFlags.None, new(ImGui.GetColumnWidth(), 30)))
                                    {
                                        var point = railSceneObj.RailPoints[cidx];
                                        ChangeHandler.ToggleObjectSelection(window, scn!.History, point.PickingId,
                                            !window.Keyboard?.IsCtrlPressed() ?? true);
                                        window.CameraToObject(point);
                                    }
                                    ImGui.PushStyleColor(ImGuiCol.Button, 0);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
                                    ImGui.TableSetColumnIndex(0);
                                    ImGui.PushID("ScenePointView" + cidx);
                                    if (ImGuiWidgets.HoverButton(IconUtils.MAG_GLASS, new(ImGui.GetColumnWidth(), 30)))
                                    {
                                        window.CameraToObject(railSceneObj.RailPoints[cidx]);
                                    }
                                    ImGui.PopStyleColor(3);

                                    ImGui.TableSetColumnIndex(2);
                                    if (cidx == 0) ImGui.BeginDisabled();
                                    ImGui.PushID("ScenePointUP" + cidx);
                                    if (ImGuiWidgets.HoverButton(IconUtils.ARROW_UP, new(ImGui.GetColumnWidth(), 30), cidx == 0))
                                    {
                                        if (railSceneObj.RailPoints.IndexOf(railSceneObj.RailPoints[cidx]) > 0) 
                                            delay = railSceneObj.RailPoints[cidx];
                                        n = true;
                                    }
                                    if (cidx == 0) ImGui.EndDisabled();
                                    ImGui.TableSetColumnIndex(3);
                                    if (cidx == railSceneObj.RailPoints.Count - 1) ImGui.BeginDisabled();
                                    ImGui.PushID("ScenePointDOWN" + cidx);
                                    if (ImGuiWidgets.HoverButton(IconUtils.ARROW_DOWN, new(ImGui.GetColumnWidth(), 30), cidx == railSceneObj.RailPoints.Count - 1))
                                    {
                                        if (railSceneObj.RailPoints.IndexOf(railSceneObj.RailPoints[cidx]) < railSceneObj.RailPoints.Count - 1) 
                                            delay = railSceneObj.RailPoints[cidx];
                                    }
                                    if (cidx == railSceneObj.RailPoints.Count - 1) ImGui.EndDisabled();

                                    cidx++;
                                }
                                ImGui.EndTable();
                            }
                            if (!autoResize) ImGui.Spacing();
                            if (delay != null) ChangeHandler.ChangeMovePoint(window, window.CurrentScene.History, railSceneObj, delay, railSceneObj.RailPoints.IndexOf(delay) + (n ? -1 : 1));

                            if (sceneObj is RailPointSceneObj) PosDrag.Use("Position", ref (sceneObj as RailPointSceneObj)!.RailPoint.Point0Trans, ref sceneObj, 10);
                            else if (sceneObj is RailHandleSceneObj) PosDrag.Use("Position", ref (sceneObj as RailHandleSceneObj)!.Offset, ref sceneObj, 10);

                            ImGui.PopItemWidth();
                            ImGui.EndChild();
                        }
                    }
                    if (sceneObj is not RailSceneObj)
                    {
                        string pnt = (sceneObj is RailPointSceneObj) ? $"Point" : $"Handle";
                        if (ImGui.CollapsingHeader($"{pnt} Translation", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            ImGui.BeginChild("trls", default, ImGuiChildFlags.AutoResizeY);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);

                            if (sceneObj is RailPointSceneObj) PosDrag.Use("Position", ref (sceneObj as RailPointSceneObj)!.RailPoint.Point0Trans, ref sceneObj, 10);
                            else if (sceneObj is RailHandleSceneObj) PosDrag.Use("Position", ref (sceneObj as RailHandleSceneObj)!.Offset, ref sceneObj, 10);

                            ImGui.PopItemWidth();
                            ImGui.EndChild();
                        }
                    }
                    if (sceneObj is RailPointSceneObj)
                    {
                        if (ImGui.CollapsingHeader($"Point Handles", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            ImGui.BeginChild("handless", default, ImGuiChildFlags.AutoResizeY);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);
                            
                            bool autoResize = railObj.Points.Count < 8;
                            if (ImGui.BeginTable("handletable", 2,
                                ImGuiTableFlags.RowBg
                                | ImGuiTableFlags.BordersOuter
                                | ImGuiTableFlags.BordersV
                                | ImGuiTableFlags.ScrollY, new(ImGui.GetWindowWidth() - style.WindowPadding.X, -1)))
                            {
                                ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                                ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.None);
                                ImGui.TableSetupColumn("Handle", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                                ImGui.TableHeadersRow();
                                if (autoResize) ImGui.SetScrollY(0);
                                for (int cc = 0; cc < 2; cc++)
                                {
                                    ImGui.TableNextRow();

                                    ImGui.PushStyleColor(ImGuiCol.Button, 0);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
                                    ImGui.TableSetColumnIndex(0);
                                    ImGui.PushID("SceneHandleView" + cc);
                                    if (ImGui.Button(IconUtils.MAG_GLASS, new(-1, 25)))
                                    {
                                        var hndl = cc == 0 ? (sceneObj as RailPointSceneObj)!.Handle1 : (sceneObj as RailPointSceneObj)!.Handle2;
                                        window.CameraToObject(hndl!);
                                    }
                                    ImGui.PopStyleColor(3);

                                    ImGui.TableSetColumnIndex(1);
                                    ImGui.PushID("SceneHandleSelectable" + cc);
                                    if (ImGui.Selectable(cc.ToString(), false))
                                    {
                                        var hndl = cc == 0 ? (sceneObj as RailPointSceneObj)!.Handle1 : (sceneObj as RailPointSceneObj)!.Handle2;
                                        ChangeHandler.ToggleObjectSelection(window, scn!.History,
                                            hndl!.PickingId,
                                            !window.Keyboard?.IsCtrlPressed() ?? true
                                        );
                                        window.CameraToObject(hndl!);
                                    }
                                }
                                ImGui.EndTable();
                            }
                            ImGui.PopItemWidth();
                            ImGui.EndChild();
                        }
                    }
                    if (sceneObj is not RailHandleSceneObj)
                    {
                        string pnt = (sceneObj is RailSceneObj) ? "Rail" : "Point";
                        if (ImGui.CollapsingHeader($"{pnt} Arguments", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            ImGui.BeginChild("arg", default, ImGuiChildFlags.AutoResizeY);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            ImGui.PushItemWidth(ImGui.GetWindowWidth() - style.WindowPadding.X * 2 - PROP_WIDTH / 2);
                            foreach (var (name, property) in (sceneObj is RailSceneObj) ? railObj.Properties : (sceneObj as RailPointSceneObj)!.RailPoint.Properties)
                            {
                                if (property is null)
                                {
                                    ImGui.TextDisabled(name);
                                    return;
                                }
                                if (!name.Contains("Arg")) continue;
                                
                                switch (property)
                                {
                                    case int:
                                        int intBuf = (int)(property ?? -1);
                                        if (sceneObj is RailSceneObj) InputIntProperties(name, ref intBuf, 1, ref railObj);
                                        else 
                                        {
                                            RailPoint rpoint = (sceneObj as RailPointSceneObj)!.RailPoint;
                                            InputIntProperties(name, ref intBuf, 1, ref rpoint);
                                        }
                                        break;
                                }
                            
                            }
                            ImGui.PopItemWidth();
                            ImGui.EndChild();
                        }
                        if (sceneObj is not RailPointSceneObj)
                        if (ImGui.CollapsingHeader("Extra Properties", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                            ImGui.BeginChild("prp", default, ImGuiChildFlags.AutoResizeY);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                            ImGui.PushItemWidth(prevW - PROP_WIDTH);

                            foreach (var (name, property) in railObj.Properties)
                            {
                                if (property is null)
                                {
                                    ImGui.TextDisabled(name);
                                    continue;
                                }
                                if (name.Contains("Arg") || name == "Priority" || name == "ShapeModelNo") continue;

                                if (ImGui.Button(IconUtils.TRASH + "##rmp" + name))
                                {
                                    railObj.Properties.Remove(name);
                                    continue;
                                }
                                ImGui.SameLine(default, style.ItemInnerSpacing.X);
                                if (ImGui.Button(IconUtils.PENCIL + "##edit" + name))
                                {
                                    window.SetupExtraPropsDialog(railObj, name);
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
                                            ChangeHandler.ChangeDictionaryValue(scn?.History!, railObj.Properties, name, intBuf, i);
                                        }
                                        break;
                                    case object p when p is float:
                                        float flBuf = (float)(p ?? -1);
                                        float f = flBuf;
                                        if (ImGui.InputFloat("##" + name + "i", ref f, 1, default, default, ImGuiInputTextFlags.EnterReturnsTrue))
                                        {
                                            ChangeHandler.ChangeDictionaryValue(scn!.History, railObj.Properties, name, flBuf, f);
                                        }
                                        break;
                                    case object p when p is string:
                                        string strBuf = (string)(p ?? string.Empty);
                                        string st = strBuf;
                                        if (ImGui.InputText("##" + name + "i", ref st, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                                        {
                                            ChangeHandler.ChangeDictionaryValue(scn!.History, railObj.Properties, name, strBuf, st);
                                        }
                                        break;
                                    case object p when p is bool:
                                        bool bl = (bool)(p ?? false);
                                        bool b = bl;
                                        if (ImGui.Checkbox("##" + name + "i", ref b))
                                        {
                                            ChangeHandler.ChangeDictionaryValue(scn!.History, railObj.Properties, name, bl, b);
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
                                window.SetupExtraPropsDialogNew(railObj);
                            }
                            ImGui.PopItemWidth();
                            ImGui.EndChild();
                        }
                    }
                }
                ImGui.EndChild();
            }
        }
        else
        {
            // Multiple objects selected:
            ImGui.TextDisabled("Multiple objects selected.");
            InputText("Layer", ref multiselector.Layer, 30, ref multiselector);
            InputInt("ViewId", ref multiselector.ViewId, 1, ref multiselector);
            //InputInt("CameraId", ref multiselector.CameraId, 1, ref multiselector);
            ImGui.Text("CameraId"); ImGui.SameLine();
            var cameraslinks = scn.Stage.CameraParams.Cameras.Where(x => x.Category == StageCamera.CameraCategory.Object).ToList();
            string[] cameraStrings = new string[cameraslinks.Count + 1];
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

            foreach (ISceneObj obj in scn.SelectedObjects)
            {
                if (obj is not IStageSceneObj stageScene) continue;

                if (multiselector.ViewId != mView)
                    stageScene.StageObj.ViewId = multiselector.ViewId;
                if (multiselector.CameraId != mCamera)
                    stageScene.StageObj.CameraId = multiselector.CameraId;
                if (multiselector.ClippingGroupId != mClip)
                    stageScene.StageObj.ClippingGroupId = multiselector.ClippingGroupId;
                if (multiselector.Layer != mLayer)
                    stageScene.StageObj.Layer = multiselector.Layer;
            }
        }

        //ImGui.PopStyleColor();
        ImGui.End();
    }

    #region Property Components


    float SetTitle(string title, string? tooltip)
    {
        ImGui.SetWindowFontScale(1.20f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4);
        float r = ImGui.GetCursorPosY();
        ImGui.Text(title);
        ImGui.SetWindowFontScale(1.0f);
        if (tooltip != null) ImGui.SetItemTooltip(tooltip);
        ImGui.Separator();
        //Child begins after this
        return r;
    }

    #endregion


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

                if (sto is IStageSceneObj stageSceneObj)
                {
                    switch (str)
                    {
                        case "Position":
                        case "Translation":
                            stageSceneObj.StageObj.Translation = rV;
                            stageSceneObj.UpdateTransform();
                            stageSceneObj.StageObj.Translation = tmprf;
                            break;

                        case "Rotation":
                            stageSceneObj.StageObj.Rotation = rV;
                            stageSceneObj.UpdateTransform();
                            stageSceneObj.StageObj.Rotation = tmprf;
                            break;

                        case "Scale":
                            stageSceneObj.StageObj.Scale = rV;
                            stageSceneObj.UpdateTransform();
                            stageSceneObj.StageObj.Scale = tmprf;
                            break;
                    }
                }
                else if (sto is RailPointSceneObj rps && str == "Position")
                {
                    rps.RailPoint.Point0Trans = rV;
                    rps.UpdateModel();
                    rps.RailPoint.Point0Trans = tmprf;
                }
                else if (sto is RailHandleSceneObj rhs && str == "Position")
                {
                    rhs.Offset = rV;
                    rhs.UpdateTransform();
                    rhs.Offset = tmprf;
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

                if (sto is IStageSceneObj stageSceneObj)
                    ChangeHandler.ChangeStageObjTransform(_window.CurrentScene!.History, stageSceneObj, str, rf, refVec3);
                else if (sto is RailPointSceneObj rps)
                {
                    ChangeHandler.ChangePointPosition(
                        _window.CurrentScene!.History,
                        rps,
                        rf,
                        refVec3,
                        false
                    );
                }
                else if (sto is RailHandleSceneObj rhs)
                {
                    ChangeHandler.ChangeHandleTransform(
                        _window.CurrentScene!.History,
                        rhs,
                        rf,
                        refVec3,
                        false
                    );
                }
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
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, sto.Properties, str, rf, i);
        }
        return false;
    }
    private bool InputIntProperties(string str, ref int rf, int step, ref RailObj sto)
    {
        int i = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputInt("##" + str + "i", ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, sto.Properties, str, rf, i);
        }
        return false;
    }
    private bool InputIntProperties(string str, ref int rf, int step, ref RailPoint sto)
    {
        int i = rf;

        ImGui.Text(str + ":");
        ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth(str);
        if (ImGui.InputInt("##" + str + "i", ref i, step, default, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ChangeHandler.ChangeDictionaryValue(window.CurrentScene!.History, sto.Properties, str, rf, i);
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

    private bool InputSwitch(string str, ref int rf, int step, ref IStageSceneObj sco)
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
