using System.Drawing;
using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Rail;
using Autumn.Storage;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Storage;

internal class RailPointSceneObj : ISceneObj
{
    public RailSceneObj ParentRail { private set; get; }
    public RailPoint RailPoint { get; }

    public RailPointType PointType => ParentRail.RailObj.PointType;
    public RailHandleSceneObj? Handle1 { get; init; }
    public RailHandleSceneObj? Handle2 { get; init; }

    public RailHandlesModel HandlesModel { get; init; }

    public Vector3 FakeRot = Vector3.Zero;
    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public RailPointSceneObj(RailPoint railPoint, RailSceneObj parent, ref uint pickingId)
    {
        ParentRail = parent;
        RailPoint = railPoint;
        PickingId = pickingId++;

        HandlesModel = new(railPoint);
        Handle1 = new(railPoint.Point1Trans-railPoint.Point0Trans, this, ref pickingId);
        Handle2 = new(railPoint.Point2Trans-railPoint.Point0Trans, this, ref pickingId);

        AABB = new(5f);

        UpdateTransform();
    }

    public void UpdateTransform()
    {
        // if (RailPoint is RailPointLinear linear)
        //     Transform = Matrix4x4.CreateTranslation(linear.Translation * 0.01f);
        // else if (RailPoint is RailPointBezier bezier)
        Transform = Matrix4x4.CreateTranslation(RailPoint.Point0Trans * 0.01f);
        if (FakeRot != Vector3.Zero)
        {
            // Apply rotation to points
            RailPoint.Point1Trans = Vector3.Transform(Handle1!.Offset, 
            Matrix4x4.CreateRotationX(  FakeRot.X * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationY( FakeRot.Y * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationZ( FakeRot.Z * (float)Math.PI / 180)) + RailPoint.Point0Trans;
            RailPoint.Point2Trans = Vector3.Transform(Handle2!.Offset, 
            Matrix4x4.CreateRotationX(  FakeRot.X * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationY( FakeRot.Y * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationZ( FakeRot.Z * (float)Math.PI / 180)) + RailPoint.Point0Trans;

            FakeRot = Vector3.Zero;
        }
        UpdateSceneHandles();
        Handle1!.UpdateTransform();
        Handle2!.UpdateTransform();
        if (ParentRail.RailModel.Initialized)
            ParentRail.UpdateModel();
        if (HandlesModel.Initialized)
            HandlesModel.UpdateModel();
    }
    public void UpdateModel()
    {
        Transform = Matrix4x4.CreateTranslation(RailPoint.Point0Trans * 0.01f);
        Handle1!.UpdateTransform();
        Handle2!.UpdateTransform();
        if (HandlesModel.Initialized)
            HandlesModel.UpdateModel();
    }
    public void UpdateModelMoving()
    {
        Transform = Matrix4x4.CreateTranslation((RailPoint.Point0Trans)* 0.01f);
        UpdateObjHandles();
        Handle1!.UpdateModelMoving();
        Handle2!.UpdateModelMoving();
        if (HandlesModel.Initialized)
            HandlesModel.UpdateModel();
    }
    public void UpdateModelRotating()
    {
        Transform = Matrix4x4.CreateTranslation((RailPoint.Point0Trans)* 0.01f);
        RailPoint.Point1Trans = Vector3.Transform(Handle1!.Offset, 
            Matrix4x4.CreateRotationX(  FakeRot.X * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationY( FakeRot.Y * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationZ( FakeRot.Z * (float)Math.PI / 180)) + RailPoint.Point0Trans;
        RailPoint.Point2Trans = Vector3.Transform(Handle2!.Offset, 
            Matrix4x4.CreateRotationX(  FakeRot.X * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationY( FakeRot.Y * (float)Math.PI / 180)
            *Matrix4x4.CreateRotationZ( FakeRot.Z * (float)Math.PI / 180)) + RailPoint.Point0Trans;

        Handle1.UpdateModelRotating();
        Handle2.UpdateModelRotating();
        if (HandlesModel.Initialized)
            HandlesModel.UpdateModel();
    }

    //public void UpdateModelTmp() => ParentRail.UpdateModelTmp();
    /// <returns>A handle or this point with the given picking ID and returns it.</returns>
    public ISceneObj? GetObjectByPickingId(uint id)
    {
        if (PickingId == id) return this;

        if (Handle1 is not null && Handle1.PickingId == id) return Handle1;

        if (Handle2 is not null && Handle2.PickingId == id) return Handle2;

        return null;
    }

    internal void UpdateObjHandle(RailHandleSceneObj handle)
    {
        if (handle == Handle1) RailPoint.Point1Trans = RailPoint.Point0Trans + handle.Offset;
        else if (handle == Handle2) RailPoint.Point2Trans = RailPoint.Point0Trans + handle.Offset;
    }
    internal void UpdateObjHandles()
    {
        RailPoint.Point1Trans = RailPoint.Point0Trans + Handle1!.Offset;
        RailPoint.Point2Trans = RailPoint.Point0Trans + Handle2!.Offset;
    }
    internal void UpdateSceneHandle(int i)
    { 
        if (i == 0) Handle1!.Offset = RailPoint.Point1Trans-RailPoint.Point0Trans;
        else if (i == 1) Handle2!.Offset = RailPoint.Point2Trans-RailPoint.Point0Trans;
    }
    internal void UpdateSceneHandles()
    { 
        Handle1!.Offset = RailPoint.Point1Trans-RailPoint.Point0Trans;
        Handle2!.Offset = RailPoint.Point2Trans-RailPoint.Point0Trans;
    }

    internal uint Clone()
    {
        throw new NotImplementedException();
    }

    public void DrawRailHandles(GL gl)
    {
        if (!HandlesModel.Initialized) HandlesModel.Initialize(gl);
        HandlesModel.Draw(gl);
    }

}
