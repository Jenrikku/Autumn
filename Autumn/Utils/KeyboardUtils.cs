using Silk.NET.Input;

namespace Autumn.Utils;

internal static class KeyboardUtils
{
    public static bool IsCtrlCombinationPressed(
        this IKeyboard keyboard,
        Key key,
        bool shift = false,
        bool alt = false
    )
    {
        if (!keyboard.IsCtrlPressed())
            return false;

        if (shift && !keyboard.IsShiftPressed())
            return false;

        if (alt && !keyboard.IsAltPressed())
            return false;

        return keyboard.IsKeyPressed(key);
    }

    public static bool IsCtrlPressed(this IKeyboard keyboard) =>
        keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);

    public static bool IsShiftPressed(this IKeyboard keyboard) =>
        keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);

    public static bool IsAltPressed(this IKeyboard keyboard) =>
        keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);
}
