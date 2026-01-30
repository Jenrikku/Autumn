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
            if (info is not DirectoryInfo dir || !dir.Name.Contains(SearchString))
                continue;

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            var flags = FileSelectableFlags;

            if (IsDirRomFS[dir.Name])
                flags |= ImGuiSelectableFlags.Disabled;

            if (ImGui.Selectable(dir.Name, false, flags))
            {
                SelectedFile = dir.Name;
                SelectedFileChanged = true;

                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    ChangeDirectory(dir.FullName);
                    break;
                }
            }

            ImGui.TableNextColumn();

            ImGui.Text(dir.LastWriteTime.ToString());
        }

        ImGui.EndTable();
    }

    protected override bool IsTargetValid()
    {
        string path = Path.Join(CurrentDirectory, SelectedFile);
        DirectoryInfo dirInfo = new(CurrentDirectory);

        if (!string.IsNullOrEmpty(SelectedFile))
        {
            if (File.Exists(path))
            {
                PathError = "The path is a file that exists.";
                return false;
            }

            if (Directory.Exists(path))
            {
                if (IsDirRomFS[SelectedFile])
                {
                    PathError = "The path already contains a project in it.";
                    return false;
                }

                if (Directory.EnumerateFileSystemEntries(path).Any()) // Directory is not empty
                {
                    PathError = "The path must be empty.";
                    return false;
                }

                return true;
            }
        }
        else if (dirInfo.EnumerateFileSystemInfos().Any()) // Directory is not empty
        {
            PathError = "The path must be empty.";
            return false;
        }

        if (dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            PathError = "The path is read only.";
            return false;
        }

        return true;
    }

    protected override void OkButtonAction()
    {
        if (string.IsNullOrEmpty(SelectedFile) || !Directory.Exists(Path.Join(CurrentDirectory, SelectedFile)))
        {
            base.OkButtonAction();
            return;
        }

        ChangeDirectory(Path.Join(CurrentDirectory, SelectedFile));
    }
}
