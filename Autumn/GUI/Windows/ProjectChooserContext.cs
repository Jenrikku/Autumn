using Autumn.Context;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal class ProjectChooserContext : FileChooserWindowContext
{
    protected const ImGuiTableFlags FileChooseFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.BordersOuterH
        | ImGuiTableFlags.Sortable
        | ImGuiTableFlags.NoSavedSettings;

    protected const ImGuiSelectableFlags FileSelectableFlags =
        ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick;

    /// <summary>
    /// Holds whether the directory in the key is a RomFS.
    /// </summary>
    protected readonly Dictionary<string, bool> IsDirRomFS = new();

    public ProjectChooserContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        SetupFileComparisons(CompareByName, CompareByDate);

        DirectoryUpdated += () =>
        {
            IsDirRomFS.Clear();

            foreach (var entry in DirectoryEntries)
            {
                if (entry is not DirectoryInfo dir)
                    continue;

                IsDirRomFS.Add(entry.Name, IsRomFS(entry.FullName));
            }
        };
    }

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

            if (ImGui.Selectable(dir.Name, false, FileSelectableFlags))
            {
                if (IsDirRomFS[dir.Name])
                {
                    SelectedFile = dir.Name;

                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        InvokeSuccessCallback(dir.FullName);
                    }
                }
                else if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
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

    /// <summary>
    /// Checks if the directory is a valid RomFS (also returns true on projects)
    /// </summary>
    private static bool IsRomFS(string dir)
    {
        string projectFile = Path.Join(dir, "autumnproj.yml");

        if (File.Exists(projectFile)) // If it's a project, then it's probably a valid RomFS
            return true;

        string stageData = Path.Join(dir, "StageData");

        if (!Directory.Exists(stageData)) // Check for StageData/
            return false;

        var files = Directory.EnumerateFiles(stageData);

        if (!files.Any()) // Check if it is not empty
            return false;

        if (SZSWrapper.ReadFile(files.First()) is null) // Check if it has the proper files
            return false;

        return true;
    }
}
