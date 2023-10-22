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

    public List<string>? PreLoadFileList { get; set; }

    public Dictionary<string, object>? AreaIdToLightNameTable { get; set; }
    public Dictionary<string, object>? CameraParam { get; set; }
    public Dictionary<string, object>? FogParam { get; set; }
    public Dictionary<string, object>? LightParam { get; set; }
    public Dictionary<string, object>? ModelToMapLightNameTable { get; set; }
    public Dictionary<string, object>? StageInfo { get; set; }
}
