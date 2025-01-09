using Autumn.Context;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal class ProjectChooserContext : FileChooserWindowContext
{
    private const ImGuiTableFlags _fileChooseFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.BordersOuterH
        | ImGuiTableFlags.Reorderable
        | ImGuiTableFlags.NoSavedSettings;

    public ProjectChooserContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager) { }

    protected override void RenderFileChoosePanel()
    {
        if (!ImGui.BeginTable("FileChoose", 3, _fileChooseFlags))
            return;

        float tableWidth = ImGui.CalcItemWidth();

        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
        ImGui.TableSetupColumn(" Name");
        ImGui.TableSetupColumn(" Size");
        ImGui.TableSetupColumn(" Modified Date");
        ImGui.TableHeadersRow();

        foreach (FileSystemInfo info in DirectoryEntries)
        {
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);

            ImGui.SetNextItemWidth(tableWidth);
            ImGui.Selectable(info.Name, false, ImGuiSelectableFlags.SpanAllColumns);

            ImGui.TableNextColumn();

            if (info is FileInfo file)
                ImGui.Text(MathUtils.ToNearestSizeUnit(file.Length));

            ImGui.TableNextColumn();

            ImGui.Text(info.LastWriteTime.ToString());
            ImGui.TableNextColumn();
        }

        ImGui.EndTable();
    }
}
