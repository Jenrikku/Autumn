
using System.Numerics;
using Autumn.Storage;

namespace Autumn.Utils
{
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
                rp[r] = new RailPoint() { Point0Trans = Vector3.UnitX , Point1Trans = (Vector3.UnitX+Vector3.UnitZ * pointDist),  Point2Trans = (Vector3.UnitX-Vector3.UnitZ* pointDist)} * dist;
                rp[r].Point0Trans = Vector3.Transform(rp[r].Point0Trans, Matrix4x4.CreateRotationY(360.0f/N * (float)Math.PI / 180 * r));
                rp[r].Point1Trans = Vector3.Transform(rp[r].Point1Trans, Matrix4x4.CreateRotationY(360.0f/N * (float)Math.PI / 180 * r));
                rp[r].Point2Trans = Vector3.Transform(rp[r].Point2Trans, Matrix4x4.CreateRotationY(360.0f/N * (float)Math.PI / 180 * r));
                
            }
            return rp;             
        }
        internal static RailPoint[] Circle(float dist = 1.0f)
        {
            return Circle(4, dist);           
        }
    }
}