using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal struct AreaStageObj : IStageObj
{
    public const string InfoName = "AreaObjInfo";

    public AreaStageObj() => Array.Fill(Args, -1);

    public int[] Args { get; init; } = new int[8];

    public string LayerName { get; set; } = "共通";
    public string MultiFileName { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";

    public int ID { get; set; } = -1;

    public Vector3 Translation { get; set; } = new(0);
    public Vector3 Rotation { get; set; } = new(0);
    public Vector3 Scale { get; set; } = new(0);

    public int SwitchA { get; set; } = -1;
    public int SwitchAppear { get; set; } = -1;
    public int SwitchB { get; set; } = -1;
    public int SwitchKill { get; set; } = -1;

    public int AreaParent { get; set; } = -1;
    public int Priority { get; set; } = -1;
    public int ShapeModelNo { get; set; } = -1;

    public Dictionary<string, object?> OtherProperties { get; } = new();

    public StageObjFileType FileType { get; set; } = StageObjFileType.Map;
}
