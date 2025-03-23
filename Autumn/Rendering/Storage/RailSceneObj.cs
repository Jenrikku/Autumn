using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Rail;
using Autumn.Storage;

namespace Autumn.Rendering.Storage;

internal class RailSceneObj : ISceneObj
{
    internal record struct PointPickingId(uint P0, uint P1, uint P2);
    internal record struct PointSelected(bool P0, bool P1, bool P2);
    internal record struct PointTransform(Matrix4x4 P0, Matrix4x4 P1, Matrix4x4 P2);

    public RailObj RailObj { get; }
    public RailModel RailModel { get; }

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool IsVisible { get; set; } = true;

    public List<PointPickingId> PointsPickingIds { get; init; }
    public List<PointSelected> PointsSelected { get; init; }
    public List<PointTransform> PointTransforms { get; init; }

    StageObj ISceneObj.StageObj => RailObj;

    public RailSceneObj(RailObj rail, RailModel railModel, ref uint pickingId)
    {
        RailObj = rail;
        RailModel = railModel;
        PickingId = pickingId++;

        AABB = new(20f); // TO-DO

        int pointCount = rail.Points.Count;

        PointsPickingIds = new(pointCount);
        PointsSelected = [.. new PointSelected[pointCount]]; // All set to false

        switch (rail.PointType)
        {
            case RailPointType.Bezier:

                for (int i = 0; i < pointCount; i++)
                    PointsPickingIds.Add(new(pickingId++, pickingId++, pickingId++));

                break;

            case RailPointType.Linear:

                for (int i = 0; i < pointCount; i++)
                    PointsPickingIds.Add(new(uint.MaxValue, pickingId++, uint.MaxValue));

                break;
        }

        PointTransforms = new(pointCount);

        UpdateTransform();
    }

    public void UpdateTransform()
    {
        Transform = Matrix4x4.Identity;

        PointTransforms.Clear();

        switch (RailObj.PointType)
        {
            case RailPointType.Bezier:

                foreach (var point in RailObj.Points.Cast<RailPointBezier>())
                {
                    Matrix4x4 t0 = Matrix4x4.CreateTranslation(point.Point0Trans * 0.01f);
                    Matrix4x4 t1 = Matrix4x4.CreateTranslation(point.Point1Trans * 0.01f);
                    Matrix4x4 t2 = Matrix4x4.CreateTranslation(point.Point2Trans * 0.01f);

                    PointTransforms.Add(new(t0, t1, t2));
                }

                break;

            case RailPointType.Linear:

                foreach (var point in RailObj.Points.Cast<RailPointLinear>())
                {
                    Matrix4x4 translate = Matrix4x4.CreateTranslation(point.Translation * 0.01f);
                    Matrix4x4 identity = Matrix4x4.Identity;

                    PointTransforms.Add(new(translate, identity, identity));
                }

                break;
        }
    }
}
