using Autumn.Context;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal class SingleFileChooserContext : FileChooserWindowContext
{
    private const ImGuiTableFlags _fileChooseFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.BordersOuterH
        | ImGuiTableFlags.Sortable
        | ImGuiTableFlags.NoSavedSettings;

    private const ImGuiSelectableFlags _fileSelectableFlags =
        ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick;

    public SingleFileChooserContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        SetupFileComparisons(CompareByName, CompareBySize, CompareByDate);
    }

    protected override unsafe void RenderFileChoosePanel()
    {
        if (!ImGui.BeginTable("FileChoose", 3, _fileChooseFlags))
            return;

        UpdateFileSortByTable(ImGui.TableGetSortSpecs());

        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
        ImGui.TableSetupColumn(" Name");
        ImGui.TableSetupColumn(" Size");
        ImGui.TableSetupColumn(" Modified Date");
        ImGui.TableHeadersRow();

        foreach (FileSystemInfo info in DirectoryEntries)
        {
            if (!info.Name.Contains(SearchString))
                continue;

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            if (ImGui.Selectable(info.Name, false, _fileSelectableFlags))
            {
                if (info is DirectoryInfo dir && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    ChangeDirectory(dir.FullName);
                    break;
                }

                if (info is FileInfo file)
                {
                    SelectedFile = file.Name.Replace(";", "\\;").Replace("\\", "\\\\");

                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        InvokeSuccessCallback(file.FullName);
                }
            }

            ImGui.TableNextColumn();

            if (info is FileInfo fileInfo)
                ImGui.Text(MathUtils.ToNearestSizeUnit(fileInfo.Length));
            else
                ImGui.Text("N/A"); // ImGui crashes if removed

            ImGui.TableNextColumn();

            ImGui.Text(info.LastWriteTime.ToString());
        }

        ImGui.EndTable();
    }
}
