using SharpYaml.Serialization;

namespace Autumn.IO;

internal class YAMLWrapper
{
    public static SerializerSettings settings =
        new()
        {
            NamingConvention = new PascalNamingConvention(),
            IgnoreUnmatchedProperties = true,
            IgnoreNulls = true,
            EmitAlias = false
        };

    public static Serializer? serializer;

    public static T? Deserialize<T>(string path)
        where T : notnull
    {
        string text;

        serializer = new(settings);

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
        serializer = new(settings);

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
