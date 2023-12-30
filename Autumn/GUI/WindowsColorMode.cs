using Microsoft.Win32;
using System.Runtime.InteropServices;

using Timer = System.Timers.Timer;

namespace Autumn.GUI;

/// <summary>
/// A class that implements color mode changes to the title bar when running on Windows.
/// </summary>
internal static class WindowsColorMode
{
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern bool DwmSetWindowAttribute(
        nint handle,
        int param,
        in int value,
        int size
    );

    private static readonly Dictionary<nint, Timer> s_handleTimers = new();

    /// <summary>
    /// Starts to apply window color mode changes to the given handle.
    /// </summary>
    public static void Init(nint handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, build: 18282))
            return; // Only works on windows 10+

        const string ColorThemeKey =
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string ColorThemeValue = "SystemUsesLightTheme";

        // Set color mode the first time.
        CheckColorMode();

        void CheckColorMode()
        {
            // Get the negated value of the specified registry key.
            int value = (int?)Registry.GetValue(ColorThemeKey, ColorThemeValue, 0) ?? 0;
            value = ~value;

            // Feed the value to the window attribute.
            // Note that this will try a different position if the first one fails.
            if (!DwmSetWindowAttribute(handle, 20, value, sizeof(int)))
                DwmSetWindowAttribute(handle, 19, value, sizeof(int));
        }

        // Use a timer to check color mode every second:
        Timer timer = new(1000);
        timer.Elapsed += (o, e) => CheckColorMode();

        timer.Start();

        s_handleTimers.Add(handle, timer);
    }

    /// <summary>
    /// Stops applying window color mode changes to the given handle.
    /// </summary>
    /// <returns>Whether the operation was successful.</returns>
    public static bool Stop(nint handle)
    {
        if (!s_handleTimers.TryGetValue(handle, out Timer? timer))
            return false;

        timer.Stop();
        timer.Dispose();

        return s_handleTimers.Remove(handle);
    }
}
