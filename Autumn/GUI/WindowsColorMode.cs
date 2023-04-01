using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AutumnSceneGL.GUI {
    public static class WindowsColorMode {
        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern bool DwmSetWindowAttribute(nint handle, int param, in int value, int size);

        public static void Init(nint handle) {
            if(!OperatingSystem.IsWindowsVersionAtLeast(10, build: 18282))
                return; // Only works on windows 10+

            const string ColorThemeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string ColorThemeValue = "SystemUsesLightTheme";

            // Set color mode the first time:
            CheckColorMode();

            void CheckColorMode() {
                int value = (int?) Registry.GetValue(ColorThemeKey, ColorThemeValue, 0) ?? 0;
                value = ~value;

                if(!DwmSetWindowAttribute(handle, 20, value, sizeof(int)))
                    DwmSetWindowAttribute(handle, 19, value, sizeof(int));
            }

            // Use a timer to check color mode every second:
            System.Timers.Timer timer = new(1000);
            timer.Elapsed += (o, e) => CheckColorMode();

            timer.Start();
        }
    }
}
