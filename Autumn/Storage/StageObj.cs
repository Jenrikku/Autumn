using System.Numerics;
using Autumn.Enums;

namespace Autumn.Storage;

internal class StageObj
{
    public StageObjType Type { get; set; }
    public StageFileType FileType { get; set; }

    public Vector3 Translation = new();
    public Vector3 Rotation = new();
    public Vector3 Scale = new(1);

    public string Name = "StageObj";
    public string? ClassName = null; // Only set when using custom classnames
    public string Layer = "共通";

    public StageObj? Parent { get; set; }
    public RailObj? Rail { get; set; }

    public List<StageObj>? Children { get; set; }

    public Dictionary<string, object?> Properties { get; init; } = new();

    public StageObj(StageObj? parent = null) => Parent = parent;
}
