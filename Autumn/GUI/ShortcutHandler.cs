using Autumn.Commands;
using Autumn.IO;
using ImGuiNET;

namespace Autumn.GUI;

internal static class ShortcutHandler
{
    private static readonly Dictionary<CommandID, ImGuiKey[]> s_commandShortcuts = new();

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

                    s_commandShortcuts.TryAdd(id, keys);
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
                CommandHandler.CallCommand(id);
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
        SetCommandShortcut(CommandID.NewProject, ImGuiKey.ModCtrl, ImGuiKey.N);
        SetCommandShortcut(CommandID.OpenProject, ImGuiKey.ModCtrl, ImGuiKey.O);

        void SetCommandShortcut(CommandID id, params ImGuiKey[] keys)
        {
            if (!overwrite && s_commandShortcuts.ContainsKey(id))
                return;

            s_commandShortcuts.Add(id, keys);
        }
    }
}
