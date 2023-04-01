using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutumnSceneGL.IO {
    internal class YAMLWrapper {
        public static T? Desearialize<T>(string path) where T : notnull {
            string text;

            try {
                text = File.ReadAllText(path);
            } catch { return default; }

            IDeserializer deserializer = new DeserializerBuilder()
                                         .WithNamingConvention(PascalCaseNamingConvention.Instance)
                                         .Build();

            return deserializer.Deserialize<T>(text);
        }

        public static void Serialize<T>(string path, T obj) where T : notnull {
            ISerializer serializer = new SerializerBuilder()
                                     .WithNamingConvention(PascalCaseNamingConvention.Instance)
                                     .Build();

            string? dir = Path.GetDirectoryName(path);

            if(dir is not null)
                Directory.CreateDirectory(dir);

            using StreamWriter writer = new(path);

            serializer.Serialize(writer, obj, typeof(T));
        }
    }
}
