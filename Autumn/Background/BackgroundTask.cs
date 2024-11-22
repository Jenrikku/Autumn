using Autumn.Enums;

namespace Autumn.Background;

internal record BackgroundTask(
    string Message,
    Action<BackgroundManager> Action,
    BackgroundTaskPriority Priority
);
