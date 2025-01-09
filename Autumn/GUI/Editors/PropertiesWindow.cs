using System.Diagnostics;
using Autumn.GUI;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Wrappers;
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

            bool databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                !string.IsNullOrEmpty(stageObj.ClassName) ? stageObj.ClassName : GetClassFromCCNT(stageObj.Name),
                out ClassDatabaseWrapper.DatabaseEntry dbEntry
            );

            if (databaseHasEntry)
            {
                if (!string.IsNullOrEmpty(dbEntry.Name))
                    ImGui.Text(dbEntry.Name);

                string tooltip = "";
                if (dbEntry.Description != null)
                    tooltip += dbEntry.Description + "\n";
                if (dbEntry.DescriptionAdditional != null)
                    tooltip += dbEntry.DescriptionAdditional;
                if (!string.IsNullOrEmpty(tooltip))
                {
                    ImGui.SameLine();
                    ImGuiWidgets.HelpTooltip(tooltip);
                }
            }

            ImGui.InputText(stageObj is RailObj ? "Name" : "ObjectName", ref stageObj.Name, 128);
            if (stageObj is not RailObj)
            {
                if (window.ContextHandler.Settings.UseClassNames)
                {
                    string ccntClass = GetClassFromCCNT(stageObj.Name);

                    _classNameBuffer = string.IsNullOrEmpty(stageObj.ClassName) ? "" : stageObj.ClassName;

                    if (ImGui.InputTextWithHint("ClassName", ccntClass, ref _classNameBuffer, 128))
                    {
                        stageObj.ClassName = ccntClass == _classNameBuffer ? null : string.IsNullOrEmpty(_classNameBuffer) ? null : _classNameBuffer;
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

                string displayName = name;
                string tooltip = "";

                if (databaseHasEntry)
                {
                    if (dbEntry.Args != null && dbEntry.Args.TryGetValue(name, out var argEntry) && argEntry.Name != null)
                    {
                        displayName = argEntry.Name;
                        if (argEntry.Default != null)
                            tooltip += "Default Value: " + argEntry.Default.ToString() + "\n";
                        if (argEntry.Default != null)
                            tooltip += "Is Required: " + (argEntry.Required ? "Yes" : "No") + "\n";
                        if (argEntry.Description != null)
                            tooltip += argEntry.Description;
                    }
                }

                switch (property)
                {
                    case object p when p is int:
                        int intBuf = (int)(p ?? -1);
                        if (ImGui.InputInt(displayName, ref intBuf))
                            stageObj.Properties[name] = intBuf;

                        if (!string.IsNullOrEmpty(tooltip))
                        {
                            ImGui.SameLine();
                            ImGuiWidgets.HelpTooltip(tooltip);
                        }

                        break;

                    case object p when p is string:
                        string strBuf = (string)(p ?? string.Empty);
                        if (ImGui.InputText(displayName, ref strBuf, 128))
                            stageObj.Properties[name] = strBuf;

                        if (!string.IsNullOrEmpty(tooltip))
                        {
                            ImGui.SameLine();
                            ImGuiWidgets.HelpTooltip(tooltip);
                        }

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
