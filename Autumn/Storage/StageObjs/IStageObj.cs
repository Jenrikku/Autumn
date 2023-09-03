using System.Numerics;

namespace Autumn.Storage.StageObjs;

internal interface IStageObj
{
    public string LayerName { get; set; }
    public string Name { get; set; }

    public Vector3 Translation { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Scale { get; set; }

    public Dictionary<string, object?> OtherProperties { get; }

    public StageObjFileType FileType { get; set; }
}

internal enum StageObjFileType : byte
{
    Map,
    Design,
    Sound
}
