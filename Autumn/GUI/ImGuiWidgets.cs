using System.Numerics;
using Autumn.Commands;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

internal static class ImGuiWidgets
{
    public static void DirectoryPathSelector(
        ref string input,
        ref bool isValidPath,
        float? width = null,
        string label = " ",
        string? dialogTitle = null,
        string? dialogDefaultPath = null
    )
    {
        float x = ImGui.GetCursorPosX();

        if (width.HasValue)
            ImGui.SetNextItemWidth(width.Value - 20);

        if (InputTextRedWhenInvalid(label, ref input, 512, !isValidPath))
            isValidPath = Directory.Exists(input);

        ImGui.SameLine();

        if (
            ImGui.Button("Select", new(50, 0))
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

    public static bool CommandMenuItem(CommandID id)
    {
        Command? command = CommandHandler.GetCommand(id);

        if (command is null)
            return false;

        if (ImGui.MenuItem(command.DisplayName, command.DisplayShortcut, false, command.Enabled))
        {
            command.Action.Invoke();
            return true;
        }

        return false;
    }

    public static bool InputTextRedWhenInvalid(
        string label,
        ref string buf,
        uint buf_size,
        bool isInvalid
    )
    {
        if (isInvalid)
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));

        bool result = ImGui.InputText(label, ref buf, buf_size);

        if (isInvalid)
            ImGui.PopStyleColor();

        return result;
    }

    public static bool InputTextRedWhenEmpty(string label, ref string buf, uint buf_size) =>
        InputTextRedWhenEqualsTo(label, ref buf, buf_size, invalidValue: string.Empty);

    public static bool InputTextRedWhenEqualsTo(
        string label,
        ref string buf,
        uint buf_size,
        string invalidValue
    ) => InputTextRedWhenInvalid(label, ref buf, buf_size, buf == invalidValue);

    public static bool InputTextRedWhenEqualsTo(
        string label,
        ref string buf,
        uint buf_size,
        IEnumerable<string> invalidValues
    )
    {
        bool isInvalid = false;

        foreach (string invalidValue in invalidValues)
            isInvalid |= buf == invalidValue;

        return InputTextRedWhenInvalid(label, ref buf, buf_size, isInvalid);
    }
}
