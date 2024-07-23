using SharpYaml.Serialization;

namespace Autumn.Wrappers;

internal static class YAMLWrapper
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
        T? result;

        try
        {
            text = File.ReadAllText(path);
            result = serializer.Deserialize<T>(text);
        }
        catch
        {
            return default;
        }

        return result;
    }

    /// <returns>Whether the object was serialized correctly.</returns>
    public static bool Serialize<T>(string path, T obj)
        where T : notnull
    {
        Serializer serializer = new(s_settings);

        string? dir = Path.GetDirectoryName(path);

        try
        {
            if (dir is not null)
                Directory.CreateDirectory(dir);

            StreamWriter writer = new(path) { AutoFlush = true };
            serializer.Serialize(writer, obj, typeof(T));
            writer.Dispose();
        }
        catch
        {
            return false;
        }

        return true;
    }
}
