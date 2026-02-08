using System.Numerics;

namespace Autumn.Rendering.Storage;

internal class RailHandleSceneObj : ISceneObj
{
    public RailPointSceneObj ParentPoint { private set; get; }
    public Vector3 Offset = new();

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public RailHandleSceneObj(Vector3 translation, RailPointSceneObj parent, ref uint pickingId)
    {
        Offset = translation;
        AABB = new(5f); // TODO
        PickingId = pickingId++;
        ParentPoint = parent;
        UpdateTransform();
    }


    public void UpdateTransform()
    {
        UpdateHandle();
        Transform = Matrix4x4.CreateTranslation((ParentPoint.RailPoint.Point0Trans + Offset) * 0.01f);
        ParentPoint.ParentRail.UpdateModelTmp();
        if (ParentPoint.HandlesModel.Initialized) ParentPoint.HandlesModel.UpdateModel();
    }

    public void UpdateModelMoving()
    {
        Transform = Matrix4x4.CreateTranslation((ParentPoint.RailPoint.Point0Trans + Offset) * 0.01f);
        ParentPoint.ParentRail.UpdateModelTmp();
        if (ParentPoint.HandlesModel.Initialized) ParentPoint.HandlesModel.UpdateModel();
    }
    public void UpdateModelRotating()
    {
        Transform = Matrix4x4.CreateTranslation((ParentPoint.RailPoint.Point0Trans + Vector3.Transform(Offset, 
         Matrix4x4.CreateRotationX( ParentPoint.FakeRotation.X * (float)Math.PI / 180)
        *Matrix4x4.CreateRotationY( ParentPoint.FakeRotation.Y * (float)Math.PI / 180)
        *Matrix4x4.CreateRotationZ( ParentPoint.FakeRotation.Z * (float)Math.PI / 180))
        ) * 0.01f);
        ParentPoint.ParentRail.UpdateModelTmp();
        if (ParentPoint.HandlesModel.Initialized) ParentPoint.HandlesModel.UpdateModel();
    }

    public void UpdateHandle() => ParentPoint.UpdateObjHandle(this);

}