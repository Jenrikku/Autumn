using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;

namespace Autumn.Rendering.Storage;

internal class RailPointSceneObj : ISceneObj
{
    private readonly Action? _railUpdate;

    public RailPoint RailPoint { get; }

    public RailPointType PointType { get; }

    public RailHandleSceneObj? Handle1 { get; init; }
    public RailHandleSceneObj? Handle2 { get; init; }

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public RailPointSceneObj(RailPoint railPoint, Action railUpdate, ref uint pickingId)
    {
        PointType = railPoint is RailPointLinear ? RailPointType.Linear : RailPointType.Bezier;
        RailPoint = railPoint;
        PickingId = pickingId++;

        if (railPoint is RailPointBezier bezier)
        {
            Handle1 = new(bezier.Point0Trans, railUpdate, ref pickingId);
            Handle2 = new(bezier.Point2Trans, railUpdate, ref pickingId);
        }

        AABB = new(1f); // TO-DO

        UpdateTransform();

        _railUpdate = railUpdate; // Important: Putting it at the end prevents it from being called when constructing
    }

    public void UpdateTransform()
    {
        if (RailPoint is RailPointLinear linear)
            Transform = Matrix4x4.CreateTranslation(linear.Translation * 0.01f);
        else if (RailPoint is RailPointBezier bezier)
            Transform = Matrix4x4.CreateTranslation(bezier.Point0Trans * 0.01f);

        _railUpdate?.Invoke();
    }

    /// <returns>A handle or this point with the given picking ID and returns it.</returns>
    public ISceneObj? GetObjectByPickingId(uint id)
    {
        if (PickingId == id) return this;

        if (Handle1 is not null && Handle1.PickingId == id) return Handle1;

        if (Handle2 is not null && Handle2.PickingId == id) return Handle2;

        return null;
    }
}
