using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct StartStageObj : IStageObj
{
    public const string InfoName = "StartInfo";

    public StartStageObj() { }

    public string LayerName { get; set; } = "共通";
    public string MultiFileName { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";

    public int MarioNo { get; set; } = -1;

    public Vector3 Translation { get; set; } = new(0);
    public Vector3 Rotation { get; set; } = new(0);
    public Vector3 Scale { get; set; } = new(1);

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
