using Autumn.GUI;
using ImGuiNET;

namespace Autumn;

internal class PropertiesWindow
{
    public static void Render(MainWindowContext context)
    {
        if (!ImGui.Begin("Properties"))
            return;

        ImGui.TextDisabled("Work in progress...");
        ImGui.End();
    }
}
