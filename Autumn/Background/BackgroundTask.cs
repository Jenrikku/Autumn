namespace Autumn.Background;

internal record BackgroundTask(string Message, Action Action, BackgroundTaskPriority Priority);

/// <summary>
/// An enum that defines the priority of the <see cref="BackgroundTask"/>.<br />
/// The higher the priority, the sooner these tasks will be done.<br />
/// Tasks with a High priority or higher will prevent the program from closing.
/// </summary>
internal enum BackgroundTaskPriority
{
    Regular,
    High,
    Highest
}
