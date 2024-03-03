using Autumn.Commands;
using Autumn.IO;
using ImGuiNET;

namespace Autumn.GUI;

internal static class ShortcutHandler
{
    private static readonly SortedDictionary<CommandID, ImGuiKey[]> s_commandShortcuts = new();

    public static void LoadFromSettings()
    {
        s_commandShortcuts.Clear();

        var dict = SettingsHandler.GetValue<Dictionary<string, ImGuiKey[]>>("KeyboardShortcuts");
        dict ??= new();

        foreach (var (name, keys) in dict)
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

                    SetCommandShortcut(true, id, keys);
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
        foreach (var (id, keys) in s_commandShortcuts)
        {
            if (IsShortcutTriggered(keys))
                CommandHandler.CallCommand(id, WindowManager.GetFocusedWindow());
        }

        static bool IsShortcutTriggered(ImGuiKey[] keys)
        {
            foreach (ImGuiKey key in keys)
            {
                if (!ImGui.IsKeyPressed(key))
                    return false;
            }

            return true;
        }
    }

    /// <param name="overwrite">Already set shortcuts will only be overwritten when this is true.</param>
    public static void SetDefaultShortcuts(bool overwrite = false)
    {
        const ImGuiKey ctrl = ImGuiKey.ModCtrl;
        const ImGuiKey shift = ImGuiKey.ModShift;
        //const ImGuiKey alt = ImGuiKey.ModAlt;

        SetCommandShortcut(overwrite, CommandID.NewProject, ctrl, ImGuiKey.N);
        SetCommandShortcut(overwrite, CommandID.OpenProject, ctrl, ImGuiKey.O);
        SetCommandShortcut(overwrite, CommandID.Undo, ctrl, ImGuiKey.Z);
        SetCommandShortcut(overwrite, CommandID.Redo, ctrl, shift, ImGuiKey.Z);
    }

    private static void SetCommandShortcut(bool overwrite, CommandID id, params ImGuiKey[] keys)
    {
        if (!overwrite && s_commandShortcuts.ContainsKey(id))
            return;

        string displayShortcut = GenerateDisplayShortcut(keys);
        CommandHandler.SetDisplayShortcut(id, displayShortcut);

        s_commandShortcuts[id] = keys;
    }

    private static string GenerateDisplayShortcut(params ImGuiKey[] keys)
    {
        string result = string.Empty;

        for (int i = 0; i < keys.Length; i++)
        {
            ImGuiKey key = keys[i];
            string keyName = key.ToString();

            if (keyName.StartsWith("Mod"))
                keyName = keyName.Remove(0, 3);

            result += keyName;

            if (i < keys.Length - 1)
                result += "+";
        }

        return result;
    }
}
