using Autumn.Enums;

namespace Autumn.Storage;

internal class RailObj : StageObj
{
    public RailPointType PointType { get; set; }

    public int RailNo = 0; // set during read and save ONLY, not user-editable

    public bool Closed = false;

    public List<RailPoint> Points { get; init; } = new();

    public static bool operator ==(RailObj r, RailObj rb)
    {
        return r.RailNo == rb.RailNo && r.Closed == rb.Closed && r.Name == rb.Name;
    }

    public static bool operator !=(RailObj r, RailObj rb)
    {
        return r.RailNo != rb.RailNo || r.Closed != rb.Closed || r.Name != rb.Name;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not RailObj rail)
            return false;

        return RailNo == rail.RailNo && Closed == rail.Closed && Name == rail.Name;
    }

    public override RailObj Clone(bool keepChildren = true)
    {
        RailObj clone = new()
        {
            Type = Type,
            FileType = FileType,
            Layer = Layer,
            Name = Name,
            PointType = PointType,
            RailNo = RailNo,
            Closed = Closed
        };

        foreach (RailPoint p in Points)
        {
            if (PointType == RailPointType.Bezier)
                clone.Points.Add(new RailPoint()
                { /*ID = p.ID,*/
                    Properties = p.Properties,
                    Point0Trans = p.Point0Trans,
                    Point1Trans = p.Point1Trans,
                    Point2Trans = p.Point2Trans
                });
            else
            {
                clone.Points.Add(new RailPoint()
                { /*ID = p.ID,*/
                    Properties = p.Properties,
                    Point0Trans = p.Point0Trans,
                });
                clone.Points.Last().SetPointLinear();
            }
        }

        foreach (string s in Properties.Keys)
            clone.Properties.Add(s, Properties[s]);

        return clone;
    }
}
