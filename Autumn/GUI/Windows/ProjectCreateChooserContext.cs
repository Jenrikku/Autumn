using Autumn.Context;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal class ProjectCreateChooserContext : ProjectChooserContext
{
    public ProjectCreateChooserContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager) { }

    protected override void RenderFileChoosePanel()
    {
        if (!ImGui.BeginTable("FileChoose", 2, FileChooseFlags))
            return;

        UpdateFileSortByTable(ImGui.TableGetSortSpecs());

        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
        ImGui.TableSetupColumn(" Name");
        ImGui.TableSetupColumn(" Modified Date");
        ImGui.TableHeadersRow();

        foreach (FileSystemInfo info in DirectoryEntries)
        {
            if (info is not DirectoryInfo dir)
                continue;

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            var flags = FileSelectableFlags;

            if (IsDirRomFS[dir.Name])
                flags |= ImGuiSelectableFlags.Disabled;

            if (ImGui.Selectable(dir.Name, false, flags) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                ChangeDirectory(dir.FullName);
                break;
            }

            ImGui.TableNextColumn();

            ImGui.Text(dir.LastWriteTime.ToString());
        }

        ImGui.EndTable();
    }
}
