using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct StartEventStageObj : IStageObj
{
    public const string InfoName = "StartEventObjInfo";

    public StartEventStageObj() => Array.Fill(Args, -1);

    public int[] Args { get; init; } = new int[8];

    public string LayerName { get; set; } = "共通";
    public string MultiFileName { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";

    public Vector3 Translation { get; set; } = new(0);
    public Vector3 Rotation { get; set; } = new(0);
    public Vector3 Scale { get; set; } = new(1);

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
