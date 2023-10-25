namespace Autumn.Storage;

internal class RailObj : StageObj
{
    public RailPointType PointType { get; set; }

    public int RailNo;

    public bool Closed;

    public List<RailPoint> Points { get; init; } = new();
}

internal enum RailPointType : byte
{
    Bezier,
    Linear

    // Note that more may exist. Further observation required.
}
