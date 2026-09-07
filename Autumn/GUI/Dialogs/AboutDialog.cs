using System.Numerics;
using Hexa.NET.ImGui;

namespace Autumn.GUI.Dialogs;

internal class AboutDialog
{
    private bool _isOpened = false;
    public bool IsOpen => _isOpened;

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("About");

        Vector2 dimensions = new(450, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "About",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.Text("Autumn: A 3DL stage editor. Pre-Beta version."u8);
        ImGui.Text("Licensed under "u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.TextLinkOpenURL("GPL-3.0"u8, "https://www.gnu.org/licenses/gpl-3.0.html"u8);
        ImGui.Text("\nCredits:\n"u8);
        ImGui.Text("\t\t-  "u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.TextLinkOpenURL("Jenrikku (JkKU)"u8, "https://github.com/Jenrikku"u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.Text("  \u2014  Author."u8);
        ImGui.Text("\t\t-  Aigis  \u2014  Co-author."u8);
        ImGui.Text("\tSpecial thanks:"u8);
        ImGui.Text("\t\t-  "u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.TextLinkOpenURL("JuPaHe64"u8, "https://github.com/jupahe64"u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.Text("  \u2014  Heavily helped with rendering and shaders."u8);
        ImGui.Text("\t\t-  "u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.TextLinkOpenURL("Fruityloops"u8, "https://github.com/fruityloops1"u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.Text("  \u2014  Author of class database."u8);
        ImGui.Text("\t\t-  "u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.TextLinkOpenURL("Cyboo"u8, "https://github.com/Cy8org1"u8);
        ImGuiWidgets.SameLineNoSpacing();
        ImGui.Text("  \u2014  Contributor to class database."u8);

        ImGui.End();        
    }
}
