using Autumn.GUI;

namespace Autumn.Commands;

internal class Command
{
    public string DisplayName { get; set; }
    public string DisplayShortcut { get; set; }
    public Action<WindowContext?> Action { get; set; }
    public Predicate<WindowContext?> Enabled { get; set; }

    public Command(
        string displayName,
        string displayShortcut,
        Action<WindowContext?> action,
        Predicate<WindowContext?> enabled
    )
    {
        DisplayName = displayName;
        DisplayShortcut = displayShortcut;
        Action = action;
        Enabled = enabled;
    }
}
