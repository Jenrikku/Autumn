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

    public virtual StageObj Clone(bool keepChildren = true)
    {
        StageObj clone =
            new()
            {
                Type = Type,
                FileType = FileType,
                Layer = Layer,
                Name = Name,
                ClassName = ClassName,
                CameraId = CameraId,
                ViewId = ViewId,
                ClippingGroupId = ClippingGroupId,
                Translation = Translation,
                Scale = Scale,
                Rotation = Rotation,
                SwitchA = SwitchA,
                SwitchAppear = SwitchAppear,
                SwitchB = SwitchB,
                SwitchDeadOn = SwitchDeadOn,
                SwitchKill = SwitchKill,
                Parent = Parent
            };

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

    public bool IsArea() =>
        Type == StageObjType.Area || Type == StageObjType.CameraArea || Type == StageObjType.AreaChild;

    public List<string> GetSwitches(int sw)
    {
        List<string> ret = new(); 
        if (SwitchA == sw) ret.Add("SwitchA"); 
        if (SwitchB == sw) ret.Add("SwitchB"); 
        if (SwitchDeadOn == sw) ret.Add("SwitchDeadOn"); 
        if (SwitchAppear == sw) ret.Add("SwitchAppear"); 
        if (SwitchKill == sw) ret.Add("SwitchKill"); 
        return ret;
    }

    public static bool Compare(StageObj A, StageObj B, bool lean = false)
    {
        if (A.Name != B.Name) return false;
        if (lean)
        {
            return
            (
                A.Translation == B.Translation &&
                A.Rotation == B.Rotation &&
                A.Scale == B.Scale &&
                A.Name == B.Name
            );
        }
        else
        return
        (            
            A.ViewId == B.ViewId &&
            A.CameraId == B.CameraId &&
            A.ClippingGroupId == B.ClippingGroupId &&
            A.SwitchA == B.SwitchA &&
            A.SwitchB == B.SwitchB &&
            A.SwitchDeadOn == B.SwitchDeadOn &&
            A.SwitchAppear == B.SwitchAppear &&
            A.SwitchKill == B.SwitchKill &&
            A.Translation == B.Translation &&
            A.Rotation == B.Rotation &&
            A.Scale == B.Scale &&
            A.Name == B.Name
        );
    }
}
