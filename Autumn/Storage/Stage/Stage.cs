namespace Autumn.Storage;

internal class Stage
{
    public Stage()
        : this("NewStage", 1) { }

    public Stage(string name, byte scenario = 1)
    {
        Name = name;
        Scenario = scenario;
        Saved = false;
        Loaded = false;
    }

    public string Name { get; set; }
    public byte Scenario { get; set; }

    public bool Saved { get; set; }
    public bool Loaded { get; set; }

    public List<StageObj>? StageData { get; set; }

    public List<object?>? PreLoadFileList { get; set; }

    public Dictionary<string, Dictionary<string, object?>> OtherFiles { get; } = new();
}
