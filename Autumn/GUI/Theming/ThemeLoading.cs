using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using ImGuiNET;
using Tomlyn;
using Tomlyn.Model;

namespace Autumn.GUI.Theming;

internal static class ThemeLoader
{
    public static string BaseThemePath { get; } = Path.Join(Directory.GetCurrentDirectory(), "Resources", "Themes");
    public static string UserThemeSuffix { get; } = "themes";

    public static IEnumerable<string> EnumerateAllThemeNames(string settingsPath)
    {
        HashSet<string> systemThemes = new(4);

        foreach (string file in Directory.EnumerateFiles(BaseThemePath).Where(x => x.EndsWith(".toml")))
        {
            systemThemes.Add(file);
            yield return Path.GetFileNameWithoutExtension(file);
        }

        string userThemeDir = Path.Join(settingsPath, UserThemeSuffix);
        if (!Directory.Exists(userThemeDir)) yield break;

        foreach (string file in Directory.EnumerateFiles(userThemeDir).Where(x => x.EndsWith(".toml") && !systemThemes.Contains(x)))
            yield return Path.GetFileNameWithoutExtension(file);
    }

    public static string? GetThemeFullPathByName(string themeName, string? settingsPath = null)
    {
        string path;

        if (settingsPath is not null)
        { 
            path = Path.Join(settingsPath, UserThemeSuffix, themeName) + ".toml";
            if (File.Exists(path)) return path;
        }

        path = Path.Join(BaseThemePath, themeName) + ".toml";
        if (File.Exists(path)) return path;

        return null;
    }

    public static bool IsValidTomlFile(string tomlPath)
    {
        long length = new FileInfo(tomlPath).Length;
        if (length > 1048576 || length <= 0) return false; // > 1MB or empty

        try
        {
            string theme = File.ReadAllText(tomlPath);
            TomlTable model = Toml.ToModel(theme);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool IsThemeReadOnly(string themeName, string settingsPath)
    {
        if (File.Exists(Path.Join(settingsPath, UserThemeSuffix, themeName) + ".toml")) return false;

        return File.Exists(Path.Join(BaseThemePath, themeName) + ".toml");
    }

    public static Theme? LoadThemeByName(string themeName, string? settingsPath = null)
    {
        string? path = GetThemeFullPathByName(themeName, settingsPath);
        if (path is null) return null;

        return LoadImGuiThemeFromToml(path);
    }

    public static unsafe Theme? LoadImGuiThemeFromToml(string tomlPath)
    {
        string themeContent;
        TomlTable model;

        try
        {
            themeContent = File.ReadAllText(tomlPath);
            model = Toml.ToModel(themeContent);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Could not load theme from {tomlPath}: {e.Message}");
            return null;
        }

        Theme theme = new();

        ImGuiStyle* stylePtr = ImGui.GetStyle().NativePtr;
        FieldInfo[] fields = typeof(ImGuiStyle).GetFields();

        foreach (var (key, val) in model)
        {
            if (val is TomlTable) continue; // Skip tables (dictionaries)

            FieldInfo? field = fields.FirstOrDefault(x => x.Name.ToLower() == key.ToLower());
            if (field is null) continue; // Ignore unknown fields

            nint offset = Marshal.OffsetOf<ImGuiStyle>(char.ToUpper(key[0]) + key[1..^0]);

            nint b = (nint)stylePtr;
            nint res = b + offset;

            switch (field.FieldType)
            {
                case Type t when t == typeof(float) && val is double v:
                    *(float*)res = (float)v;
                    break;

                case Type t when t == typeof(float) && val is float v:
                    *(float*)res = v;
                    break;

                case Type t when t == typeof(Vector2) && val is TomlArray v:
                    for (int i = 0; i < v.Count && i < 2; i++)
                    {
                        float r = 0;
                        if (v[i] is double d) r = (float)d;
                        if (v[i] is float f) r = f;

                        *(float*)res = r;
                        res += sizeof(float);
                    }
                    break;

                case Type t when t.IsEnum && val is string v:
                    {
                        Enum.TryParse(t, v, out object? r);
                        *(int*)res = (int)(r ?? 0);
                    }
                    break;
            }
        }

        if (model.TryGetValue("colors", out object cols) && cols is TomlTable colors)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            string[] colorNames = Enum.GetNames<ImGuiCol>();

            foreach (var (key, val) in colors)
            {
                int color = Array.FindIndex(colorNames, x => x.ToLower() == key.ToLower());
                if (color < 0) continue; // Ignore unknown colors
                style.Colors[color] = readColor(val);
            }
        }

        if (model.TryGetValue("extras", out object extr) && extr is TomlTable extras)
        {
            theme.AxisXColor = readColorByKey(extras, "AxisXColor");
            theme.AxisYColor = readColorByKey(extras, "AxisYColor");
            theme.AxisZColor = readColorByKey(extras, "AxisZColor");
        }

        theme.ImGuiStyle = *ImGui.GetStyle().NativePtr;
        return theme;

        static Vector4 readColorByKey(TomlTable table, string key)
        {
            if (!table.TryGetValue(key, out object val)) return new() { W = 1.0f };
            return readColor(val);
        }

        static Vector4 readColor(object val)
        {
            Vector4 res = new() { W = 1.0f };

            if (val is TomlArray arr && arr.Count >= 3)
            {
                Span<float> rgba = stackalloc float[4];
                rgba[3] = 1.0f;

                for (byte i = 0; i < rgba.Length && i < arr.Count; i++)
                {
                    switch (arr[i])
                    {
                        case object x when x is float v:
                            rgba[i] = v;
                            break;

                        case object x when x is double v:
                            rgba[i] = (float)v;
                            break;

                        case object x when x is int v:
                            rgba[i] = v / 255f;
                            break;

                        case object x when x is long v:
                            rgba[i] = v / 255f;
                            break;
                    }
                }

                res = new(rgba);
            }
            else if (val is string col && col.StartsWith("rgba"))
            {
                string[] comps = col[5..^1].Split(", ");

                if (comps.Length < 4) return res; // Ignore incorrectly formatted colors

                int.TryParse(comps[0], out int r);
                int.TryParse(comps[1], out int g);
                int.TryParse(comps[2], out int b);
                float.TryParse(comps[3], CultureInfo.InvariantCulture, out float a);

                res = new(r / 255f, g / 255f, b / 255f, a);
            }

            return res;
        }
    }
}
