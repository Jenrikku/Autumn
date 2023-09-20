using System.Numerics;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

public static class ImGuiWidgets
{
    public static void DirectoryPathSelector(
        ref string input,
        ref bool isValidPath,
        float? width = null,
        string? dialogTitle = null,
        string? dialogDefaultPath = null
    )
    {
        float x = ImGui.GetCursorPosX();

        if (width.HasValue)
            ImGui.SetNextItemWidth(width.Value - 20);

        if (ImGui.InputText(" ", ref input, 512))
            isValidPath = Directory.Exists(input);

        ImGui.SameLine();

        if (
            ImGui.Button("Select", new(20, 0))
            && TinyFileDialogs.SelectFolderDialog(
                out string? dialogOutput,
                dialogTitle,
                dialogDefaultPath
            )
        )
        {
            input = dialogOutput;
            isValidPath = Directory.Exists(input);
        }

        if (!isValidPath && !string.IsNullOrEmpty(input))
        {
            ImGui.SetCursorPosX(x);

            ImGui.TextColored(
                new Vector4(0.8549f, 0.7254f, 0.2078f, 1),
                "The path does not exist."
            );
        }
    }
}