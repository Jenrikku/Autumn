using Autumn.IO;
using SharpYaml.Serialization;

namespace Autumn.Storage;

internal struct Project
{
    public Project() { }

    public string Name { get; set; } = string.Empty;
    public ushort Version { get; set; } = ProjectHandler.SupportedVersion;
    public string BuildOutput { get; set; } = string.Empty;
    public SortedDictionary<string, object> Settings { get; set; } = new();

    [YamlIgnore]
    public string? SavePath { get; set; } // (Directory)

    [YamlIgnore]
    public string ProjectFileName { get; set; } = "autumnproj.yml";

    [YamlIgnore]
    public List<Stage> Stages { get; } = new();

    [YamlIgnore]
    public Dictionary<string, ActorObj> Objects { get; } = new();
}
