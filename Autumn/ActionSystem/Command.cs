using Autumn.GUI.Windows;

namespace Autumn.ActionSystem;

internal class Command
{
    /// <summary>
    /// The name this command takes inside a menu item.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// What this command will affect / the general category
    /// </summary>
    public CommandCategory Category { get; set; }
    public enum CommandCategory
    {
        General,
        Rail,
        Selection,
        Transform,
    }

    /// <summary>
    /// The action to perform when the command is triggered.
    /// The window context represents the focused window.
    /// </summary>
    public Action<WindowContext?> Action { get; set; }

    /// <summary>
    /// The condition that must happen in order to trigger the shortcut.
    /// The window context represents the focused window.
    /// </summary>
    public Predicate<WindowContext?> Enabled { get; set; }

    public Command(
        string displayName,
        Action<WindowContext?> action,
        Predicate<WindowContext?> enabled,
        CommandCategory cat = CommandCategory.General
    )
    {
        DisplayName = displayName;
        Action = action;
        Enabled = enabled;
        Category = cat;
    }
}
