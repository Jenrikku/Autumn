using Autumn.GUI;

namespace Autumn.Commands;

internal class Command
{
    public string DisplayName { get; set; }
    public string DisplayShortcut { get; set; } = string.Empty;
    public Action<WindowContext?> Action { get; set; }
    public Predicate<WindowContext?> Enabled { get; set; }

    public Command(
        string displayName,
        Action<WindowContext?> action,
        Predicate<WindowContext?> enabled
    )
    {
        DisplayName = displayName;
        Action = action;
        Enabled = enabled;
    }
}
