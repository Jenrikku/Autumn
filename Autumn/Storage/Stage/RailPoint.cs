using System.Numerics;

namespace Autumn.Storage;

internal abstract class RailPoint
{
    public int ID = -1;

    public Dictionary<string, object?> Properties { get; init; } = new();
}

internal class RailPointBezier : RailPoint
{
    public Vector3 Point0Trans = new();
    public Vector3 Point1Trans = new();
    public Vector3 Point2Trans = new();
}

internal class RailPointLinear : RailPoint
{
    public Vector3 Translation = new();
}
