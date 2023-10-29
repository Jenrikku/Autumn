namespace Autumn.Storage;

internal class RailObj : StageObj
{
    public RailPointType PointType { get; set; }

    public int RailNo = 0;

    public bool Closed = false;

    public List<RailPoint> Points { get; init; } = new();
}

internal enum RailPointType : byte
{
    Bezier,
    Linear

    // Note that more may exist. Further observation required.
}
