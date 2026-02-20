using System.Numerics;
using Autumn.Storage;

namespace Autumn.Utils;

public static class RailDefaults
{
    internal static RailPoint[] Line(float dist = 1.0f)
    {
        return [new RailPoint() { Point0Trans = Vector3.UnitX * dist}, new RailPoint() { Point0Trans = -System.Numerics.Vector3.UnitX  * dist}];             
    }

    internal static RailPoint[] Circle(int N, float dist = 1.0f, float pointDist = 0.5f)
    {
        RailPoint[] rp = new RailPoint[N];
        for (int r = 0; r < N; r++)
        {
            rp[r] = new RailPoint() { Point0Trans = Vector3.UnitX, Point1Trans = (Vector3.UnitX + Vector3.UnitZ * pointDist), Point2Trans = (Vector3.UnitX - Vector3.UnitZ * pointDist) } * dist;
            rp[r].Point0Trans = Vector3.Transform(rp[r].Point0Trans, Matrix4x4.CreateRotationY(360.0f / N * (float)Math.PI / 180 * r));
            rp[r].Point1Trans = Vector3.Transform(rp[r].Point1Trans, Matrix4x4.CreateRotationY(360.0f / N * (float)Math.PI / 180 * r));
            rp[r].Point2Trans = Vector3.Transform(rp[r].Point2Trans, Matrix4x4.CreateRotationY(360.0f / N * (float)Math.PI / 180 * r));
        }
        return rp;
    }

    internal static RailPoint[] Circle(float dist = 1.0f, float pointDist = 0.5f)
    {
        return Circle(4, dist, pointDist);
    }

    internal static RailPoint[] Rectangle(float width = 3.0f, float length = 2.0f)
    {
        RailPoint[] rp = new RailPoint[4];
        rp[0] = new RailPoint() { Point0Trans = width * Vector3.UnitX + length * Vector3.UnitZ, };
        rp[1] = new RailPoint() { Point0Trans = width * Vector3.UnitX - length * Vector3.UnitZ };
        rp[2] = new RailPoint() { Point0Trans = width * -Vector3.UnitX - length * Vector3.UnitZ };
        rp[3] = new RailPoint() { Point0Trans = width * -Vector3.UnitX + length * Vector3.UnitZ };

        for (int i = 0; i < 4; i++)
        {
            rp[i].Point1Trans = rp[i].Point0Trans;
            rp[i].Point2Trans = rp[i].Point0Trans;
        }

        return rp;
    }
}
