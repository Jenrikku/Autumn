namespace Autumn.Storage;

internal struct Stage
{
    public Stage()
        : this("NewStage", 1) { }

    public Stage(string name, byte scenario = 1)
    {
        Name = name;
        Scenario = scenario;
    }

    public string Name { get; set; }
    public byte Scenario { get; set; }

    public List<StageObj>? StageObjs { get; set; }

    public List<string>? PreLoadFileList { get; set; }

    public Dictionary<string, object>? AreaIdToLightNameTable { get; set; }
    public Dictionary<string, object>? CameraParam { get; set; }
    public Dictionary<string, object>? FogParam { get; set; }
    public Dictionary<string, object>? LightParam { get; set; }
    public Dictionary<string, object>? ModelToMapLightNameTable { get; set; }
    public Dictionary<string, object>? StageInfo { get; set; }
}
