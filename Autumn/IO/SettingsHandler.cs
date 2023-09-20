using Autumn.IO;
using static System.Environment;

namespace Autumn.GUI;

internal static class SettingsHandler
{
    public static Dictionary<string, object> Settings { get; private set; } = new();

    private static string? _settingsPath = null;
    public static string SettingsPath
    {
        get
        {
            if (string.IsNullOrEmpty(_settingsPath))
            {
                string home = GetFolderPath(SpecialFolder.UserProfile);
                string config = Path.Join(home, ".config");

                if (!Directory.Exists(config))
                    Directory.CreateDirectory(config).Attributes |= FileAttributes.Hidden;

                _settingsPath = Path.Join(config, "autumn", "config.yml");
                Directory.CreateDirectory(_settingsPath);
            }

            return _settingsPath;
        }
        set => _settingsPath = value;
    }

    public static void ReadSettings() =>
        Settings = YAMLWrapper.Desearialize<Dictionary<string, object>>(SettingsPath) ?? new();

    public static void SaveSettings() => YAMLWrapper.Serialize(SettingsPath, Settings);

    public static T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (Settings.TryGetValue(key, out object? obj) && obj is T result)
            return result;

        return defaultValue;
    }

    public static void SetValue(string key, object value)
    {
        if (!Settings.TryAdd(key, value))
            Settings[key] = value;
    }
}
