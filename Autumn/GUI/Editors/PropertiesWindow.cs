using System.Diagnostics;
using Autumn.GUI;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn;

internal class PropertiesWindow(MainWindowContext window)
{
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
            ImGui.InputText(stageObj is RailObj ? "Name" : "ObjectName", ref stageObj.Name, 128);
            if (stageObj is not RailObj)
            {
                if (window.ContextHandler.Settings.UseClassNames)
                {
                    string hint = string.Empty;

                    if (string.IsNullOrEmpty(stageObj.ClassName))
                        hint = GetClassFromCCNT(stageObj.Name);

                    stageObj.ClassName ??= ""; // So that input text works well.

                    ImGui.InputTextWithHint("ClassName", hint, ref stageObj.ClassName, 128);
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

            ImGui.InputText("Layer", ref stageObj.Layer, 30);

            if (ImGui.DragFloat3("Translation", ref stageObj.Translation, v_speed: 10))
                sceneObj.UpdateTransform();

            if (ImGui.DragFloat3("Rotation", ref stageObj.Rotation, v_speed: 2))
                sceneObj.UpdateTransform();

            if (ImGui.DragFloat3("Scale", ref stageObj.Scale, v_speed: 0.2f))
                sceneObj.UpdateTransform();

            foreach (var (name, property) in stageObj.Properties)
            {
                if (property is null)
                {
                    ImGui.TextDisabled(name);
                    return;
                }

                switch (property)
                {
                    case object p when p is int:
                        int intBuf = (int)(p ?? -1);
                        if (ImGui.InputInt(name, ref intBuf))
                            stageObj.Properties[name] = intBuf;

                        break;

                    case object p when p is string:
                        string strBuf = (string)(p ?? string.Empty);
                        if (ImGui.InputText(name, ref strBuf, 128))
                            stageObj.Properties[name] = strBuf;

                        break;

                    default:
                        throw new NotImplementedException(
                            "The property type " + property?.GetType().FullName
                                ?? "null" + " is not supported."
                        );
                }
            }
            if (stageObj.Type == Enums.StageObjType.Regular 
                || stageObj.Type == Enums.StageObjType.Area 
                || stageObj.Type == Enums.StageObjType.Child 
                || stageObj.Type == Enums.StageObjType.AreaChild)
            {
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
                                window.CurrentScene!.Camera.LookFrom(s.StageObj.Translation*0.01f, aabb.GetDiagonal()*0.01f);      
                            }
                        }
                    }
                }
                else
                {
                    ImGui.TextDisabled("No parent assigned.");
                }
                ImGui.Text("Children: ");
                if (stageObj.Children != null && stageObj.Children.Any())
                {
                    ImGui.SameLine(); 
                    if(ImGui.Button("Edit children"))
                    {
                        window._editChildrenDialog = new(window, stageObj);
                        window._editChildrenDialog.Open();
                    }

                    ImGui.BeginTable("childrenTable", 2,
                                                        ImGuiTableFlags.RowBg
                                                        | ImGuiTableFlags.BordersOuter
                                                        | ImGuiTableFlags.BordersV
                                                        | ImGuiTableFlags.Resizable);
                    ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                    ImGui.TableSetupColumn("Object");
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
                    ImGui.TableHeadersRow();
                    
                    foreach (SceneObj obj in window.CurrentScene!.EnumerateSceneObjs())
                    {
                        StageObj sObj = obj.StageObj;

                        if (sObj.Type != Enums.StageObjType.Child && sObj.Type != Enums.StageObjType.AreaChild) continue;
                        if (sObj.Parent != stageObj) continue;
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);

                        ImGui.PushID("SceneChildSelectable" + obj.PickingId);
                        if (ImGui.Selectable(sObj.Name, false)) 
                        {
                            ChangeHandler.ToggleObjectSelection(
                                window,
                                window.CurrentScene.History,
                                obj,
                                !window.Keyboard?.IsCtrlPressed() ?? true
                            );
                            AxisAlignedBoundingBox aabb = obj.Actor.AABB * sObj.Scale;
                            window.CurrentScene!.Camera.LookFrom(sObj.Translation*0.01f, aabb.GetDiagonal()*0.01f);
                        }

                        ImGui.TableNextColumn();

                        ImGui.Text(sObj.Type.ToString());
                    }

                    ImGui.EndTable();
                }
                else
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("No children assigned.");
                    ImGui.SameLine();
                    if(ImGui.Button("Edit children"))
                    {
                        window._editChildrenDialog = new(window, stageObj);
                        window._editChildrenDialog.Open();
                    }
                }
            }

            if (oldName != stageObj.Name)
            {
                sceneObj.UpdateActor(window.ContextHandler.FSHandler, window.GLTaskScheduler);
            }
        }
        else
        {
            // Multiple objects selected:
        }

        ImGui.End();
    }

    private string GetClassFromCCNT(string objectName)
    {
        var table = window.ContextHandler.FSHandler.ReadCreatorClassNameTable();

        if (!table.TryGetValue(objectName, out string? className))
            return "NotFound";

        return className;
    }
}
