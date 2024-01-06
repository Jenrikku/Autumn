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

    // For object types that have no ID, the value must be -1.
    public int ID = -1;
    public int ParentID = -1;
    public int RailID = -1;

    public List<StageObj>? Children { get; set; }

    public Dictionary<string, object?> Properties { get; init; } = new();

    private RailObj? _rail;
    private StageObj? _parent;

    public StageObj? GetParent(IEnumerable<StageObj> stageData)
    {
        if (_parent is not null)
        {
            ParentID = _parent.ID;
            return _parent;
        }

        foreach (StageObj obj in stageData)
            if (obj.ID == ParentID)
            {
                _parent = obj;
                return obj;
            }

        return null;
    }

    public RailObj? GetRail(IEnumerable<StageObj> stageData)
    {
        if (_rail is not null)
        {
            RailID = _rail.ID;
            return _rail;
        }

        foreach (StageObj obj in stageData)
            if (obj.ID == RailID && obj is RailObj rail)
            {
                _rail = rail;
                return rail;
            }

        return null;
    }

    public void SetParent(StageObj? parent)
    {
        ParentID = parent?.ID ?? -1;
        _parent = parent;
    }

    public void SetRail(RailObj? rail)
    {
        RailID = rail?.ID ?? -1;
        _rail = rail;
    }
}

internal enum StageObjType : byte
{
    Regular = 0,
    Area,
    CameraArea,
    Goal,
    StartEvent,
    Start,
    DemoScene,
    Rail,
    AreaChild,
    Child
}

internal enum StageObjFileType : byte
{
    Map,
    Design,
    Sound
}
