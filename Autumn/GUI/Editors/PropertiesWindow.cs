using System.Diagnostics;
using Autumn.GUI;
using Autumn.IO;
using Autumn.Scene;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn;

internal static class PropertiesWindow
{
    public static void Render(MainWindowContext context)
    {
        if (!ImGui.Begin("Properties"))
            return;

        if (context.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            ImGui.End();
            return;
        }

        List<SceneObj> selectedObjects = context.CurrentScene.SelectedObjects;
        int selectedCount = selectedObjects.Count;

        if (selectedCount <= 0)
        {
            ImGui.TextDisabled("No object is selected.");
            ImGui.End();
            return;
        }

        if (selectedCount == 1)
        {
            // Only one object selected:
            SceneObj sceneObj = selectedObjects[0];
            StageObj stageObj = sceneObj.StageObj;

            ImGui.InputText(stageObj is RailObj ? "Name" : "ObjectName", ref stageObj.Name, 128);
            if (stageObj is not RailObj)
            {
                if (ProjectHandler.UseClassNames)
                {
                    Debug.Assert(stageObj.ClassName is not null);
                    ImGuiWidgets.InputTextRedWhenEmpty("ClassName", ref stageObj.ClassName, 128);
                }
                else
                {
                    Debug.Assert(stageObj.ClassName is null);

                    RomFSHandler.CreatorClassNameTable.TryGetValue(
                        stageObj.Name,
                        out string? className
                    );

                    string name = className ?? "NotFound";
                    ImGui.InputText("ClassName", ref name, 128, ImGuiInputTextFlags.ReadOnly);
                }
            }

            ImGui.InputText("Layer", ref stageObj.Layer, 30);

            if (stageObj.ID != -1)
                if (ImGui.InputInt("ID", ref stageObj.ID) && stageObj.ID < 0)
                    stageObj.ID = 0;

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
}
