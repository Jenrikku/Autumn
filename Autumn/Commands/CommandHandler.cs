namespace Autumn.Commands;

internal static class CommandHandler
{
    private static bool s_isInitilized = false;

    private static readonly Dictionary<CommandID, Command> s_commands = new();

    public static void Initialize()
    {
        if (s_isInitilized)
            return;

        s_commands.Add(CommandID.NewProject, CommandGenerator.NewProject());
        s_commands.Add(CommandID.OpenProject, CommandGenerator.OpenProject());

        s_isInitilized = true;
    }

    public static Dictionary<CommandID, Command>.Enumerator EnumerateCommands() =>
        s_commands.GetEnumerator();

    public static void CallCommand(CommandID id)
    {
        if (!s_commands.TryGetValue(id, out Command? command))
            return;

        command.Action.Invoke();
    }

    public static Command? GetCommand(CommandID id)
    {
        if (!s_commands.TryGetValue(id, out Command? command))
            return null;

        return command;
    }

    public static void SetDisplayName(CommandID id, string displayName)
    {
        Command? command = GetCommand(id);

        if (command is not null)
            command.DisplayName = displayName;
    }

    public static void SetDisplayShortcut(CommandID id, string displayShortcut)
    {
        Command? command = GetCommand(id);

        if (command is not null)
            command.DisplayName = displayShortcut;
    }

    public static void SetEnabled(CommandID id, bool enabled)
    {
        Command? command = GetCommand(id);

        if (command is not null)
            command.Enabled = enabled;
    }
}
