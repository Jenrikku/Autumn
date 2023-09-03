using System.Runtime.InteropServices;

namespace TinyFileDialogsSharp;

internal static unsafe partial class Native
{
    const string lib = "tinyfiledialogs";

    // aCharVariableName: "tinyfd_version" "tinyfd_needs" "tinyfd_response"
    // aIntVariableName : "tinyfd_verbose" "tinyfd_silent" "tinyfd_allowCursesDialogs"
    //                    "tinyfd_forceConsole" "tinyfd_assumeGraphicDisplay" "tinyfd_winUtf8"

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial string? tinyfd_getGlobalChar(string aCharVariableName); // returns NULL on error

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tinyfd_getGlobalInt(string aIntVariableName); // returns -1 on error

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tinyfd_setGlobalInt(string aIntVariableName, int aValue); // returns -1 on error

    [LibraryImport(lib)]
    public static partial void tinyfd_beep();

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tinyfd_notifyPopup(
        string? aTitle, // NULL or ""
        string? aMessage, // NULL or "" may contain \n \t
        string? aIconType // "info" "warning" "error"
    ); // return has only meaning for tinyfd_query

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tinyfd_messageBox(
        string? aTitle, // NULL or ""
        string? aMessage, // NULL or "" may contain \n \t
        string? aDialogType, // "ok" "okcancel" "yesno" "yesnocancel"
        string? aIconType, // "info" "warning" "error" "question"
        int aDefaultButton // 0 for cancel/no, 1 for ok/yes, 2 for no in yesnocancel
    ); // returns 0 for cancel/no, 1 for ok/yes, 2 for no in yesnocancel

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial string? tinyfd_inputBox(
        string? aTitle, // NULL or ""
        string? aMessage, // NULL or "" (\n and \t have no effect)
        string? aDefaultInput // NULL = passwordBox, "" = inputbox
    ); // returns NULL on cancel

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial string? tinyfd_saveFileDialog(
        string? aTitle, // NULL or ""
        string? aDefaultPathAndFile, // NULL or ""
        int aNumOfFilterPatterns, // 0  (1 in the following example)
        string[]? aFilterPatterns, // NULL or char const * lFilterPatterns[1]={"*.txt"}
        string? aSingleFilterDescription // NULL or "text files"
    ); // returns NULL on cancel

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial string? tinyfd_openFileDialog(
        string? aTitle, // NULL or ""
        string? aDefaultPathAndFile, // NULL or ""
        int aNumOfFilterPatterns, // 0 (2 in the following example)
        string[]? aFilterPatterns, // NULL or char const * lFilterPatterns[2]={"*.png","*.jpg"};
        string? aSingleFilterDescription, // NULL or "image files"
        int aAllowMultipleSelects // 0 or 1
    ); // returns NULL on cancel, in case of multiple files, the separator is |

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int tinyfd_notifyPopupW(
        string? aTitle, // NULL or L""
        string? aMessage, // NULL or L"" may contain \n \t
        string? aIconType // L"info" L"warning" L"error"
    ); // return has only meaning for tinyfd_query

    [LibraryImport(lib, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int tinyfd_messageBoxW(
        string? aTitle, // NULL or L""
        string? aMessage, // NULL or L"" may contain \n \t
        string? aDialogType, // L"ok" L"okcancel" L"yesno" L"yesnocancel"
        string? aIconType, // L"info" L"warning" L"error" L"question"
        int aDefaultButton // 0 for cancel/no, 1 for ok/yes, 2 for no in yesnocancel
    ); // returns 0 for cancel/no, 1 for ok/yes, 2 for no in yesnocancel
}
