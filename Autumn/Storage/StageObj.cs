using System.Numerics;

namespace Autumn.Storage;

internal class StageObj
{
    public StageObjType Type { get; set; }
    public StageObjFileType FileType { get; set; }

    public Vector3 Translation = new();
    public Vector3 Rotation = new();
    public Vector3 Scale = new(1);

    public string Name = "StageObj";
    public string Layer = "共通";

    // For object types that have no ID, the value should be -1.
    public int ID = -1;

    public Dictionary<string, StageObjProperty> Properties { get; init; } = new();
}

internal enum StageObjType : byte
{
    Regular = 0,
    Area,
    CameraArea,
    Goal,
    StartEvent,
    Start

    // Note that more may exist. Further observation required.
}

internal enum StageObjFileType : byte
{
    Map,
    Design,
    Sound
}
