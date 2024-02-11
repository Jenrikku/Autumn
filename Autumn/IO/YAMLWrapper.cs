using SharpYaml.Serialization;

namespace Autumn.IO;

internal class YAMLWrapper
{
    private static readonly SerializerSettings s_settings =
        new()
        {
            NamingConvention = new PascalNamingConvention(),
            IgnoreUnmatchedProperties = true,
            IgnoreNulls = true,
            EmitAlias = false
        };

    public static T? Deserialize<T>(string path)
        where T : notnull
    {
        string text;
        Serializer serializer = new(s_settings);

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
        Serializer serializer = new(s_settings);

        string? dir = Path.GetDirectoryName(path);

        if (dir is not null)
            Directory.CreateDirectory(dir);

        StreamWriter writer;

        try
        {
            writer = new(path) { AutoFlush = true };
        }
        catch
        {
            return;
        }

        serializer.Serialize(writer, obj, typeof(T));

        writer.Dispose();
    }
}
