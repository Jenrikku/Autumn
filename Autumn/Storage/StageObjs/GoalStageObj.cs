using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct GoalStageObj : IStageObj
{
    public const string InfoName = "GoalObjInfo";

    public GoalStageObj() => Array.Fill(Args, -1);

    public int[] Args { get; init; } = new int[8];

    public string LayerName { get; set; } = "共通";
    public string MultiFileName { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";

    public int ID { get; set; } = -1;
    public int CameraId { get; set; } = -1;
    public int ClippingGroupId { get; set; } = -1;

    public Vector3 Translation { get; set; } = new(0);
    public Vector3 Rotation { get; set; } = new(0);
    public Vector3 Scale { get; set; } = new(0);

    public int SwitchA { get; set; } = -1;
    public int SwitchAppear { get; set; } = -1;
    public int SwitchB { get; set; } = -1;
    public int SwitchDeadOn { get; set; } = -1;

    public int ShapeModelNo { get; set; } = -1;
    public int ViewId { get; set; } = -1;

    // public Rail Rail { get; set; }

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
