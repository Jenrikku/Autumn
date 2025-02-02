using System.Diagnostics;
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

    public int SwitchA = -1;
    public int SwitchB = -1;
    public int SwitchDeadOn = -1;
    public int SwitchAppear = -1;
    public int SwitchKill = -1;

    public Dictionary<string, object?> Properties { get; init; } = new();

    public int ViewId = -1;
    public int CameraId = -1;
    public int ClippingGroupId = -1;

    public StageObj(StageObj? parent = null) => Parent = parent;

    public StageObj Clone(bool keepChildren = true)
    {
        var clone = new StageObj();
        clone.Type = Type;
        clone.FileType = FileType;
        clone.Layer = Layer;
        clone.Name = Name;
        clone.ClassName = ClassName;
        clone.CameraId = CameraId;
        clone.ViewId = ViewId;
        clone.ClippingGroupId = ClippingGroupId;
        clone.Translation = Translation;
        clone.Scale = Scale;
        clone.Rotation = Rotation;
        clone.SwitchA = SwitchA;
        clone.SwitchAppear = SwitchAppear;
        clone.SwitchB = SwitchB;
        clone.SwitchDeadOn = SwitchDeadOn;
        clone.SwitchKill = SwitchKill;
        clone.Parent = Parent;
        foreach (string s in Properties.Keys)
        {
            clone.Properties.Add(s, Properties[s]);
        }
        if (keepChildren && Children != null && Children.Count > 0) // we would have repeat references otherwise
        {
            clone.Children = new();
            foreach (StageObj child in Children)
            {
                StageObj clonedChild = child.Clone();
                clonedChild.Parent = clone;
                clone.Children.Add(clonedChild);

            }
        }
        clone.Rail = Rail;

        return clone;
    }

}
