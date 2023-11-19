namespace Autumn.Commands;

internal class Command
{
    public string DisplayName { get; set; }
    public string DisplayShortcut { get; set; }
    public Action Action { get; set; }
    public bool Enabled { get; set; }

    public Command(string displayName, string displayShortcut, Action action, bool enabled)
    {
        DisplayName = displayName;
        DisplayShortcut = displayShortcut;
        Action = action;
        Enabled = enabled;
    }
}
