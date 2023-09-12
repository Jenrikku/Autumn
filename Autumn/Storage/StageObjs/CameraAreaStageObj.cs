using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct CameraAreaStageObj : IStageObj
{
    public const string InfoName = "CameraAreaInfo";

    public CameraAreaStageObj() { }

    public string LayerName { get; set; } = "共通";
    public string MultiFileName { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";

    public int ID { get; set; } = -1;
    public int CameraId { get; set; } = -1;

    public Vector3 Translation { get; set; } = new();
    public Vector3 Rotation { get; set; } = new();
    public Vector3 Scale { get; set; } = new(1);

    public int SwitchAppear { get; set; }
    public int SwitchDeadOn { get; set; }
    public int SwitchKill { get; set; }

    public int Priority { get; set; } = -1;
    public int ShapeModelNo { get; set; } = -1;

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
