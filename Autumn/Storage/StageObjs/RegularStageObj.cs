using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct RegularStageObj : IStageObj
{
    public const string InfoName = "ObjInfo";

    public RegularStageObj() => Array.Fill(Args, -1);

    public int[] Args { get; init; } = new int[10];

    public string LayerName { get; set; } = "共通";
    public string Name { get; set; } = "Default";

    public int ID { get; set; } = -1;
    public int CameraId { get; set; } = -1;
    public int ClippingGroupId { get; set; } = -1;

    public Vector3 Translation { get; set; } = new();
    public Vector3 Rotation { get; set; } = new();
    public Vector3 Scale { get; set; } = new(1);

    public int SwitchA { get; set; } = -1;
    public int SwitchAppear { get; set; } = -1;
    public int SwitchB { get; set; } = -1;
    public int SwitchDeadOn { get; set; } = -1;
    public int SwitchKill { get; set; } = -1;

    public int ShapeModelNo { get; set; } = -1;
    public int GenerateParent { get; set; } = -1;
    public int ViewId { get; set; } = -1;

    // public Rail Rail { get; set; }

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
