using System.Numerics;

namespace Autumn.Storage;

internal class RailPoint
{
    public Dictionary<string, object?> Properties { get; init; } = new();

    ///<summary> Reference point </summary>
    public Vector3 Point0Trans = new();

    ///<summary> Previous Handle (Handle1) </summary>
    public Vector3 Point1Trans = new();

    ///<summary> Next Handle (Handle2) </summary>
    public Vector3 Point2Trans = new();

    public void SetPointLinear()
    {
        Point1Trans = Point0Trans + Vector3.UnitX * 100;
        Point2Trans = Point0Trans - Vector3.UnitX * 100;
    }

    public static RailPoint operator *(RailPoint p, float f)
    {
        p.Point0Trans *= f;
        p.Point1Trans *= f;
        p.Point2Trans *= f;
        return p;
    }
    public RailPoint Clone()
    {
        var r = new RailPoint() { Point0Trans = this.Point0Trans, Point1Trans = this.Point1Trans, Point2Trans = this.Point2Trans };
        foreach (string s in this.Properties.Keys)
        {
            r.Properties.Add(s, this.Properties[s]);
        }
        return r;

    }
}
