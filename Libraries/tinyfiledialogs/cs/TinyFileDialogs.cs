using static TinyFileDialogsSharp.Native;
using static TinyFileDialogsSharp.Internal;
using System.Diagnostics.CodeAnalysis;

namespace TinyFileDialogsSharp;

public static class TinyFileDialogs
{
    public static string Version => tinyfd_getGlobalChar("tinyfd_version") ?? string.Empty;

    /// <summary>
    /// Defines whether tinyfiledialogs should print the command line calls.
    /// </summary>
    public static bool Verbose
    {
        get => tinyfd_getGlobalInt("tinyfd_verbose") == 1;
        set => tinyfd_setGlobalInt("tinyfd_verbose", value ? 1 : 0);
    }

    /// <summary>
    /// Defines whether tinyfiledialogs should hide any warnings or errors.
    /// </summary>
    public static bool Silent
    {
        get => tinyfd_getGlobalInt("tinyfd_silent") == 1;
        set => tinyfd_setGlobalInt("tinyfd_silent", value ? 1 : 0);
    }

    /// <summary>
    /// Asks the OS to make a "beep".
    /// </summary>
    public static void Beep() => tinyfd_beep();

    public static unsafe void NotifyPopup(
        string? title = null,
        string? message = null,
        NotifyIconType iconType = NotifyIconType.Info
    )
    {
        string iconTypeStr = iconType switch
        {
            NotifyIconType.Warning => "warning",
            NotifyIconType.Error => "error",
            _ => "info"
        };

        if (OperatingSystem.IsWindows())
            tinyfd_notifyPopupW(title, message, iconTypeStr);
        else
            tinyfd_notifyPopup(title, message, iconTypeStr);
    }

    public static unsafe MessageBoxButton MessageBox(
        string? title = null,
        string? message = null,
        DialogType dialogType = DialogType.Ok,
        MessageIconType iconType = MessageIconType.Info,
        MessageBoxButton defaultButton = MessageBoxButton.NoCancel
    )
    {
        string dialogTypeStr = dialogType switch
        {
            DialogType.OkCancel => "okcancel",
            DialogType.YesNo => "yesno",
            DialogType.YesNoCancel => "yesnocancel",
            _ => "ok"
        };

        string iconTypeStr = iconType switch
        {
            MessageIconType.Warning => "warning",
            MessageIconType.Error => "error",
            MessageIconType.Question => "question",
            _ => "info"
        };

        int result;

        if (OperatingSystem.IsWindows())
            result = tinyfd_messageBoxW(
                title,
                message,
                dialogTypeStr,
                iconTypeStr,
                (int)defaultButton
            );
        else
            result = tinyfd_messageBox(
                title,
                message,
                dialogTypeStr,
                iconTypeStr,
                (int)defaultButton
            );

        return (MessageBoxButton)result;
    }

    public static unsafe bool InputBox(
        [NotNullWhen(true)] out string? output,
        string? title = null,
        string? message = null,
        string? defaultInput = null,
        bool isPasswordInput = false
    )
    {
        if (isPasswordInput)
            defaultInput = null;
        else
            defaultInput ??= string.Empty;

        output = tinyfd_inputBox(title, message, defaultInput);
        return output is not null;
    }

    public static unsafe bool SaveFileDialog(
        [NotNullWhen(true)] out string? output,
        string? title = null,
        string? defaultPath = null,
        string[]? filterPatterns = null,
        string? filterDescription = null
    )
    {
        output = tinyfd_saveFileDialog(
            title,
            defaultPath,
            filterPatterns?.Length ?? 0,
            filterPatterns,
            filterDescription
        );

        return output is not null;
    }

    public static unsafe bool OpenFileDialog(
        [NotNullWhen(true)] out string[]? output,
        string? title = null,
        string? defaultPath = null,
        string[]? filterPatterns = null,
        string? filterDescription = null,
        bool allowMultipleSelects = false
    )
    {
        output = null;

        string? result = tinyfd_openFileDialog(
            title,
            defaultPath,
            filterPatterns?.Length ?? 0,
            filterPatterns,
            filterDescription,
            allowMultipleSelects ? 1 : 0
        );

        if (result is null)
            return false;

        output = result.Split('|');
        return true;
    }
}
