namespace TinyFileDialogsSharp;

public enum NotifyIconType
{
    Info,
    Warning,
    Error
}

public enum MessageIconType
{
    Info,
    Warning,
    Error,
    Question
}

public enum DialogType
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

public enum MessageBoxButton : int
{
    NoCancel = 0,
    OkYes = 1,

    /// <summary>
    /// Used in <see cref="DialogType.YesNoCancel"/> to select "No" by default.
    /// </summary>
    No = 2
}
