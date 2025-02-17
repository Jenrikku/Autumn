using System.Numerics;

namespace Autumn.Rendering.Storage;

internal class AxisAlignedBoundingBox
{
    public Vector3 Max = new(50, 50, 50);
    public Vector3 Min = new(-50, -50, -50);

    public AxisAlignedBoundingBox() { }

    public AxisAlignedBoundingBox(Vector3 mx, Vector3 mn)
    {
        Max = mx;
        Min = mn;
    }

    public AxisAlignedBoundingBox(float x)
    {
        Max *= x;
        Min *= x;
    }

    public float GetDiagonal()
    {
        return Vector3.Distance(Max, Min);
    }

    public static AxisAlignedBoundingBox operator *(AxisAlignedBoundingBox _aabb, float t)
    {
        return new AxisAlignedBoundingBox(_aabb.Max * t, _aabb.Min * t);
    }

    public static AxisAlignedBoundingBox operator *(AxisAlignedBoundingBox _aabb, Vector3 v)
    {
        AxisAlignedBoundingBox rAABB = new(_aabb.Max, _aabb.Min);
        rAABB.Max.X *= v.X;
        rAABB.Min.X *= v.X;
        rAABB.Max.Y *= v.Y;
        rAABB.Min.Y *= v.Y;
        rAABB.Max.Z *= v.Z;
        rAABB.Min.Z *= v.Z;
        return rAABB;
    }
}
