using SharpYaml.Serialization;

namespace Autumn.IO;

internal class YAMLWrapper
{
    public static SerializerSettings settings =
        new() { NamingConvention = new PascalNamingConvention(), IgnoreUnmatchedProperties = true };

    public static Serializer serializer = new(settings);

    public static T? Desearialize<T>(string path)
        where T : notnull
    {
        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return default;
        }

        return serializer.Deserialize<T>(text);
    }

    public static void Serialize<T>(string path, T obj)
        where T : notnull
    {
        string? dir = Path.GetDirectoryName(path);

        if (dir is not null)
            Directory.CreateDirectory(dir);

        using StreamWriter writer = new(path);

        serializer.Serialize(writer, obj, typeof(T));
    }
}
