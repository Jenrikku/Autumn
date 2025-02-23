using System.Numerics;
using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.GUI.Windows;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

internal static class ImGuiWidgets
{
    public const ImGuiDockNodeFlags NO_TAB_BAR = (ImGuiDockNodeFlags)0x1000;
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

    public static void CommandMenuItem(
        CommandID id,
        ActionHandler actionHandler,
        WindowContext context
    )
    {
        var (command, shortcut) = actionHandler.GetAction(id);

        if (command is null)
            return;

        bool clicked = ImGui.MenuItem(
            command.DisplayName,
            shortcut?.DisplayString ?? string.Empty,
            false,
            command.Enabled(context)
        );

        if (clicked)
            command.Action(context);
    }

    public static bool ArrowButton(string str, ImGuiDir dir, Vector2? size = null)
    {
        switch (dir)
        {
            case ImGuiDir.Up:
                str = "\uf062##"+str;
                break;
            case ImGuiDir.Down:
                str = "\uf063##"+str;
                break;
            case ImGuiDir.Left:
                str = "\uf060##"+str;
                break;
            case ImGuiDir.Right:
                str = "\uf061##"+str;
                break;
        }
        return ImGui.Button(str, size ?? default);
    }

    public static float SetPropertyWidth(string str)
    {
        float ret = 0;
        if (ImGui.GetWindowWidth() - (ImGui.GetWindowWidth() * 2 / 3 - ImGui.GetStyle().ItemSpacing.X / 2) > (ImGui.CalcTextSize(str + ":").X + 12))
        {
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() / 3);
            ret = float.Round(ImGui.GetWindowWidth() * 2 / 3 - ImGui.GetStyle().ItemSpacing.X / 2);
        }
        else
        {
            ret = float.Round(ImGui.GetWindowWidth() - ImGui.CalcTextSize(str + ":").X - ImGui.GetStyle().ItemSpacing.X * 2);
        }
        ImGui.SetNextItemWidth(ret);
        return ret;
    }
    public static float SetPropertyWidthGen(string str, int ratioA = 2, int ratioB = 3,  bool colon = true)
    {
        float ret = 0;
        if (ImGui.GetWindowWidth() - (ImGui.GetWindowWidth() * ratioA / ratioB - ImGui.GetStyle().ItemSpacing.X / 2) > (ImGui.CalcTextSize(str + ":").X + 12))
        {
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() * (ratioB - ratioA) / ratioB);
            ret = float.Round(ImGui.GetWindowWidth() * ratioA / ratioB - ImGui.GetStyle().ItemSpacing.X);
        }
        else
        {
            ret = float.Round(ImGui.GetWindowWidth() - ImGui.CalcTextSize(str + ":").X - ImGui.GetStyle().ItemSpacing.X * 2 - 5);
        }
        ImGui.SetNextItemWidth(ret);
        return ret;
    }

    public static void PrePropertyWidthName(string str, int ratioA = 2, int ratioB = 3,  bool colon = true)
    {
        ImGui.Text(str + (colon ? ":" : ""));
        ImGui.SameLine();
        SetPropertyWidthGen(str, ratioA, ratioB, colon);
    }


    public static void HelpTooltip(string tooltip)
    {
        Vector2 cursorPos = ImGui.GetCursorPos();

        ImGui.TextDisabled("?");
        ImGui.SetCursorPos(cursorPos);

        ImGui.InvisibleButton("helpButton", new Vector2(20, 20));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    public static bool InputTextRedWhenInvalid(
        string label,
        ref string buf,
        uint buf_size,
        bool isInvalid,
        string hint = ""
    )
    {
        if (isInvalid)
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));

        bool result = ImGui.InputTextWithHint(label, hint, ref buf, buf_size);

        if (isInvalid)
            ImGui.PopStyleColor();

        return result;
    }

    public static bool InputTextRedWhenEmpty(string label, ref string buf, uint buf_size, string hint) =>
        InputTextRedWhenInvalid(label, ref buf, buf_size, buf == string.Empty, hint);

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

    public static bool DragFloat(string str, ref float rf, float v_speed = 1)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str+":");
        return ImGui.DragFloat("##" + str, ref rf, v_speed);

    }
    public static bool DragInt(string str, ref int rf, float v_speed = 1)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str+":");
        return ImGui.DragInt("##" + str, ref rf, v_speed);

    }
    public static bool InputInt(string str, ref int rf, int step = 1)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str+":");
        return ImGui.InputInt("##" + str, ref rf, step);
    }

    internal static bool InputText(string str, ref string rf, uint max)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str+":");
        return ImGui.InputText("##" + str, ref rf, max);
    }
}
