using System.Numerics;
using Autumn.ActionSystem;
using Autumn.Enums;
using static Autumn.Utils.IconUtils;
using Autumn.GUI.Windows;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

internal static class ImGuiWidgets
{
    public const ImGuiDockNodeFlags NO_TAB_BAR = (ImGuiDockNodeFlags)0x1000;
    public const ImGuiDockNodeFlags NO_WINDOW_MENU_BUTTON = (ImGuiDockNodeFlags)(1 << 14);
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
                str = ARROW_UP + "##" + str;
                break;
            case ImGuiDir.Down:
                str = ARROW_DOWN + "##" + str;
                break;
            case ImGuiDir.Left:
                str = ARROW_LEFT + "##" + str;
                break;
            case ImGuiDir.Right:
                str = ARROW_RIGHT + "##" + str;
                break;
            case ImGuiDir.COUNT:
                str = DOWN + "##" + str;
                break;
        }
        return ImGui.Button(str, size ?? default);
    }

    /// <summary>
    /// Adds a text "header" with a line below it
    /// </summary>
    /// <param name="str"></param>
    /// <param name="scale"></param>
    /// <param name="original"></param>
    public static void TextHeader(string str, float scale = 1.2f, float original = 1.0f)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.Text(str);
        ImGui.SetWindowFontScale(original);
        ImGui.Separator();
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
    public static float SetPropertyWidthGen(string str, int ratioA = 2, int ratioB = 3, bool colon = true, float padding = 12)
    {
        float ret = 0;
        if (ImGui.GetWindowWidth() - (ImGui.GetWindowWidth() * ratioA / ratioB - ImGui.GetStyle().ItemSpacing.X / 2) > (ImGui.CalcTextSize(str + ":").X + padding))
        {
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() * (ratioB - ratioA) / ratioB);
            ret = float.Round(ImGui.GetWindowWidth() * ratioA / ratioB - ImGui.GetStyle().ItemSpacing.X);
        }
        else
        {
            ret = float.Round(ImGui.GetWindowWidth() - ImGui.CalcTextSize(str + ":").X - ImGui.GetStyle().ItemSpacing.X * 4);
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        return ret;
    }

    public static void PrePropertyWidthName(string str, int ratioA = 2, int ratioB = 3, bool colon = true)
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

        ImGui.InvisibleButton("helpButton", new Vector2(24, 24));
        ImGui.SetItemTooltip(tooltip);
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

    public class InputComboBox
    {
        private bool wasHovering = false;
        private bool arrowPressed = false;
        private Vector2 listpos = new();

        public bool Use(string str, ref string value, List<string> comboStrings, float width = -1)
        {
            bool ret = false;
            listpos.X = ImGui.GetCursorPosX();
            ImGui.SetNextItemWidth(width - 24);
            ImGui.InputText($"##{str}", ref value, 128);
            listpos.Y = ImGui.GetCursorPosY();
            bool textactive = ImGui.IsItemActive() || wasHovering;
            ret = ImGui.IsItemActive();

            ImGui.SameLine(default, 0);
            if (ArrowButton("arr" + str, ImGuiDir.COUNT))
            {
                arrowPressed = !arrowPressed;
                wasHovering = arrowPressed;
            }
            if (!ImGui.IsItemFocused())
            {
                arrowPressed = false || wasHovering;
            }

            string finalresult = "";
            if (value != "")
            {
                string val = value;
                comboStrings = comboStrings.Where(x => x.Contains(val, StringComparison.InvariantCultureIgnoreCase)).ToList();
            }

            int t = comboStrings.ToList().IndexOf(value);

            if ((textactive && value != "") || arrowPressed)
            {
                ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ImGuiCol.FrameBg) | 0xFF000000);
                ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.GetColorU32(ImGuiCol.FrameBg) & 0x00FFFFFF);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.SetNextItemAllowOverlap();
                ImGui.SetCursorPos(listpos);
                ImGui.SetNextItemWidth(width);
                if (ImGui.BeginChild(str, default))
                {
                    bool activ = ImGui.ListBox("##CombostringsList" + str, ref t, comboStrings.ToArray(), comboStrings.Count);

                    wasHovering = ImGui.IsItemHovered();
                    if (activ)
                    {
                        value = comboStrings[t];
                        wasHovering = false;
                        arrowPressed = false;
                        ret = true;
                    }
                }
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar();
            }
            else wasHovering = false;

            if (comboStrings.Count == 0)
                finalresult = value;
            ImGui.SetCursorPos(listpos);
            return ret;
        }
    }

    public static bool DragFloat(string str, ref float rf, float v_speed = 1)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str + ":");
        return ImGui.DragFloat("##" + str, ref rf, v_speed);

    }
    public static bool DragInt(string str, ref int rf, float v_speed = 1)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str + ":");
        return ImGui.DragInt("##" + str, ref rf, v_speed);

    }
    public static bool InputInt(string str, ref int rf, int step = 1, int ratioA = 2, int ratioB = 3)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str + ":", ratioA, ratioB);
        return ImGui.InputInt("##" + str, ref rf, step);
    }
    public static bool InputFloat(string str, ref float rf, float step = 1, int ratioA = 2, int ratioB = 3)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str + ":", ratioA, ratioB);
        return ImGui.InputFloat("##" + str, ref rf, step);
    }

    internal static bool InputText(string str, ref string rf, uint max)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        SetPropertyWidthGen(str + ":");
        return ImGui.InputText("##" + str, ref rf, max);
    }

    public static bool IsMouseHoveringRect(Vector2 v_min, Vector2 v_max)
    {
        bool r = v_max.X > ImGui.GetMousePos().X && ImGui.GetMousePos().X > v_min.X;
        r = r && v_max.Y > ImGui.GetMousePos().Y && ImGui.GetMousePos().Y > v_min.Y;
        return r;
    }
}
