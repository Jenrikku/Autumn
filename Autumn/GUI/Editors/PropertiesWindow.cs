using System.Diagnostics;
using Autumn.GUI;
using Autumn.Rendering;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn;

internal class PropertiesWindow(MainWindowContext window)
{
    string _classNameBuffer = "";
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

            ImGui.InputText(stageObj is RailObj ? "Name" : "ObjectName", ref stageObj.Name, 128);
            if (stageObj is not RailObj)
            {
                if (window.ContextHandler.Settings.UseClassNames)
                {
                    string ccntClass = GetClassFromCCNT(stageObj.Name);

                    _classNameBuffer = string.IsNullOrEmpty(stageObj.ClassName) ? "" : stageObj.ClassName;

                    if (ImGui.InputTextWithHint("ClassName", ccntClass, ref _classNameBuffer, 128))
                    {
                        stageObj.ClassName = ccntClass == _classNameBuffer ? null : _classNameBuffer;
                    }
                }
                else
                {
                    ImGui.Text("ClassName: ");
                    ImGui.SameLine();
                    ImGui.Text(GetClassFromCCNT(stageObj.Name));
                    ImGui.SameLine();
                    ImGui.Spacing();

                    ImGuiWidgets.HelpTooltip(
                        "The class name is picked from CreatorClassNameTable.szs"
                    );
                }
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
