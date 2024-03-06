using Autumn.Commands;
using Autumn.IO;
using ImGuiNET;

namespace Autumn.GUI;

internal record struct Shortcut(bool Ctrl, bool Shift, bool Alt, ImGuiKey Key);

internal static class ShortcutHandler
{
    private static readonly SortedDictionary<CommandID, Shortcut> s_commandShortcuts = new();

    public static void LoadFromSettings()
    {
        s_commandShortcuts.Clear();

        var dict = SettingsHandler.GetValue<Dictionary<string, Shortcut>>("KeyboardShortcuts");
        dict ??= new();

        foreach (var (name, shortcut) in dict)
        {
            string[] splitName = name.Split('.');

            if (splitName.Length < 2)
                continue;

            switch (splitName[0])
            {
                case "Command":
                    CommandID id = Enum.Parse<CommandID>(splitName[1]);

                    if (id == CommandID.Unknown)
                        continue;

                    SetCommandShortcut(id, shortcut);
                    break;

                // More to be added.

                default:
                    throw new NotImplementedException("Unknown shortcut type");
            }
        }

        SetDefaultShortcuts();
    }

    public static void ExecuteShortcuts()
    {
        if (ImGui.GetIO().WantTextInput)
            return;

        foreach (var (id, shortcut) in s_commandShortcuts)
        {
            if (IsShortcutTriggered(shortcut))
                CommandHandler.CallCommand(id, WindowManager.GetFocusedWindow());
        }

        static bool IsShortcutTriggered(Shortcut shortcut)
        {
            bool result = true;

            result &= shortcut.Ctrl == ImGui.IsKeyDown(ImGuiKey.ModCtrl);
            result &= shortcut.Shift == ImGui.IsKeyDown(ImGuiKey.ModShift);
            result &= shortcut.Alt == ImGui.IsKeyDown(ImGuiKey.ModAlt);

            result &= ImGui.IsKeyPressed(shortcut.Key);

            return result;
        }
    }

    public static void ClearShortcuts()
    {
        foreach (var (id, _) in s_commandShortcuts)
            CommandHandler.SetDisplayShortcut(id, string.Empty);

        s_commandShortcuts.Clear();
    }

    /// <summary>
    /// Sets
    /// </summary>
    /// <param name="overwrite"></param>
    public static void SetDefaultShortcuts(bool overwrite = false)
    {
        // Define shortcuts
        Shortcut newProject = new(Ctrl: true, Shift: false, Alt: false, ImGuiKey.N),
            openProject = new(Ctrl: true, Shift: false, Alt: false, ImGuiKey.O),
            undo = new(Ctrl: true, Shift: false, Alt: false, ImGuiKey.Z),
            redo = new(Ctrl: true, Shift: true, Alt: false, ImGuiKey.Z);

        // Set shortcuts
        SetCommandShortcut(CommandID.NewProject, newProject, overwrite);
        SetCommandShortcut(CommandID.OpenProject, openProject, overwrite);
        SetCommandShortcut(CommandID.Undo, undo, overwrite);
        SetCommandShortcut(CommandID.Redo, redo, overwrite);
    }

    private static void SetCommandShortcut(CommandID id, Shortcut shortcut, bool overwrite = false)
    {
        if (!overwrite && s_commandShortcuts.ContainsKey(id))
            return;

        string displayShortcut = GenerateDisplayShortcut(shortcut);
        CommandHandler.SetDisplayShortcut(id, displayShortcut);

        s_commandShortcuts[id] = shortcut;
    }

    private static string GenerateDisplayShortcut(Shortcut shortcut)
    {
        string result = string.Empty;

        if (shortcut.Ctrl)
            result += "Ctrl+";

        if (shortcut.Shift)
            result += "Shift+";

        if (shortcut.Alt)
            result += "Alt+";

        result += shortcut.Key.ToString();

        return result;
    }
}
