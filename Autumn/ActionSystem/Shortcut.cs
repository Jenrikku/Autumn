using ImGuiNET;

namespace Autumn.ActionSystem;

internal class Shortcut
{
    public bool Ctrl;
    public bool Shift;
    public bool Alt;
    public ImGuiKey Key;

    public string DisplayString { get; }

    public Shortcut(bool ctrl, bool shift, bool alt, ImGuiKey key)
    {
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;
        Key = key;

        DisplayString = string.Empty;

        if (ctrl)
            DisplayString += "Ctrl+";

        if (shift)
            DisplayString += "Shift+";

        if (alt)
            DisplayString += "Alt+";

        DisplayString += Enum.GetName(key);
    }

    public bool IsTriggered()
    {
        if (Ctrl != ImGui.IsKeyDown(ImGuiKey.ModCtrl))
            return false;

        if (Shift != ImGui.IsKeyDown(ImGuiKey.ModShift))
            return false;

        if (Alt != ImGui.IsKeyDown(ImGuiKey.ModAlt))
            return false;

        return ImGui.IsKeyPressed(Key, false);
    }
}
