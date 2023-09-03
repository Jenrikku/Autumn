using SharpYaml.Serialization;

namespace Autumn.Storage;

internal struct Project
{
    public Project() { }

    public string Name { get; set; } = "NewProject";
    public string BuildOutput { get; set; } = string.Empty;
    public Dictionary<string, object> Settings { get; set; } = new();

    [YamlIgnore]
    public string? SavePath { get; set; } // (Directory)

    [YamlIgnore]
    public bool Saved { get; set; } = false;

    [YamlIgnore]
    public List<Stage> Stages { get; set; } = new();

    [YamlIgnore]
    public Dictionary<string, ActorObj> Objects { get; set; } = new();
}
