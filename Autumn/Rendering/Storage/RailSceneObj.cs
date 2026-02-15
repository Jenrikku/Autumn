using System.Diagnostics;
using System.Numerics;
using Autumn.Rendering.Rail;
using Autumn.Storage;
using Autumn.Utils;

namespace Autumn.Rendering.Storage;

internal class RailSceneObj : ISceneObj
{
    private readonly HashSet<uint> _assignedIds = new(); // Optimization, reduces CPU overhead when checking ids

    public void AddToHash(uint b) => _assignedIds.Add(b);
    public void RemoveFromHash(uint b) => _assignedIds.Remove(b);

    public RailObj RailObj { get; }
    public RailModel RailModel { get; }

    public List<RailPointSceneObj> RailPoints { get; } = new();
    public Vector3 Center { get; set; }
    public Vector3 FakeOffset = Vector3.Zero;
    public Vector3 FakeRotation = Vector3.Zero;
    public Vector3 FakeScale = Vector3.One;

    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;
    public AxisAlignedBoundingBox AABB { get; set; }
    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public RailSceneObj(RailObj rail, RailModel railModel, ref uint pickingId)
    {
        RailObj = rail;
        RailModel = railModel;
        PickingId = pickingId++;

        AABB = new(1);

        foreach (RailPoint railPoint in rail.Points)
        {
            uint oldId = pickingId;

            RailPointSceneObj sceneObj = new(railPoint, this, ref pickingId);
            RailPoints.Add(sceneObj);

            for (uint i = oldId; i < pickingId; i++)
                _assignedIds.Add(i);
        }
        UpdateModel();
    }

    /// <summary>
    /// Searches the rail for a point or handle with the given picking ID and returns it.
    /// </summary>
    public ISceneObj? GetObjectByPickingId(uint id)
    {
        if (PickingId == id) return this;

        if (!_assignedIds.Contains(id)) return null;

        foreach (RailPointSceneObj railPoint in RailPoints)
        {
            ISceneObj? sceneObj = railPoint.GetObjectByPickingId(id);
            if (sceneObj is not null) return sceneObj;
        }

        return null;
    }

    public void UpdateModel()
    {
        if (RailModel.Initialized)
            RailModel.UpdateModel();
        UpdateBounds();
    }
    public void UpdateModelTmp()
    {
        if (RailModel.Initialized)
            RailModel.UpdateModel();
    }

    public void UpdateBounds()
    {
        Center = Vector3.Zero;
        foreach (RailPoint p in RailObj.Points)
        {
            Center += p.Point0Trans;
        }
        Center /= (float)RailObj.Points.Count;
        AABB = new();
        AABB.Max += Center;
        AABB.Min += Center;
        foreach (RailPoint p in RailObj.Points)
        {
            if (true)//RailObj.PointType == Enums.RailPointType.Linear)
            {
                if (AABB.Max.X < p.Point0Trans.X && Center.X < p.Point0Trans.X) AABB.Max.X = p.Point0Trans.X;
                if (AABB.Max.Y < p.Point0Trans.Y && Center.Y < p.Point0Trans.Y) AABB.Max.Y = p.Point0Trans.Y;
                if (AABB.Max.Z < p.Point0Trans.Z && Center.Z < p.Point0Trans.Z) AABB.Max.Z = p.Point0Trans.Z;
                if (AABB.Min.X > p.Point0Trans.X && Center.X > p.Point0Trans.X) AABB.Min.X = p.Point0Trans.X;
                if (AABB.Min.Y > p.Point0Trans.Y && Center.Y > p.Point0Trans.Y) AABB.Min.Y = p.Point0Trans.Y;
                if (AABB.Min.Z > p.Point0Trans.Z && Center.Z > p.Point0Trans.Z) AABB.Min.Z = p.Point0Trans.Z;
            }
        }
    }

    public void UpdateTransform()
    {

    }
    public void UpdateAfterMove()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans += FakeOffset;
            r.RailPoint.Point1Trans += FakeOffset;
            r.RailPoint.Point2Trans += FakeOffset;
            r.UpdateSceneHandles();
            r.UpdateModel();
        }
        FakeOffset = Vector3.Zero;
        UpdateModel();

        // Modify the points directly after move
    }
    public void UpdateDuringMove()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans += FakeOffset;
            r.RailPoint.Point1Trans += FakeOffset;
            r.RailPoint.Point2Trans += FakeOffset;
            r.UpdateSceneHandles();
            r.UpdateModel();
        }
        UpdateModelTmp();

        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans -= FakeOffset;
            r.RailPoint.Point1Trans -= FakeOffset;
            r.RailPoint.Point2Trans -= FakeOffset;
            r.UpdateSceneHandles();
        }
    }
    public void UpdateAfterRotate()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.UpdateSceneHandles();
            r.UpdateModel();
            r.Handle1!.UpdateModelRotating();
            r.Handle2!.UpdateModelRotating();
        }
        FakeRotation = Vector3.Zero;
        UpdateModel();
        // Modify the points directly after rotate
    }
    public void UpdateDuringRotate()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center,
                Matrix4x4.CreateRotationX(FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.UpdateSceneHandles();
            r.UpdateModel();
            r.Handle1!.UpdateModelRotating();
            r.Handle2!.UpdateModelRotating();
        }
        UpdateModelTmp();
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center,
                Matrix4x4.CreateRotationX(-FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(-FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(-FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center,
                Matrix4x4.CreateRotationX(-FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(-FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(-FakeRotation.Z * (float)Math.PI / 180)) + Center;
            r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center,
                Matrix4x4.CreateRotationX(-FakeRotation.X * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationY(-FakeRotation.Y * (float)Math.PI / 180)
                * Matrix4x4.CreateRotationZ(-FakeRotation.Z * (float)Math.PI / 180)) + Center;
        }
        // Modify the points directly during rotate
    }
    public void UpdateAfterScale()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {
            r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
            r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
            r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
            r.UpdateSceneHandles();
            r.UpdateModel();
            r.Handle1!.UpdateModelRotating();
            r.Handle2!.UpdateModelRotating();
        }
        FakeScale = Vector3.One;
        UpdateModel();
        // Modify the points directly after scale
    }
    public void UpdateDuringScale()
    {
        foreach (RailPointSceneObj r in RailPoints)
        {

            if (FakeScale.X != 0 && FakeScale.Y != 0 && FakeScale.Z != 0)
            {
                r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
                r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
                r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center, Matrix4x4.CreateScale(FakeScale)) + Center;
            }
            else
            {
                r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center, Matrix4x4.CreateScale(0.01f)) + Center;
                r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center, Matrix4x4.CreateScale(0.01f)) + Center;
                r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center, Matrix4x4.CreateScale(0.01f)) + Center;
            }
            r.UpdateSceneHandles();
            r.UpdateModel();
        }
        UpdateModelTmp();
        foreach (RailPointSceneObj r in RailPoints)
        {
            if (FakeScale.X != 0 && FakeScale.Y != 0 && FakeScale.Z != 0)
            {
                r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center, Matrix4x4.CreateScale(Vector3.One / FakeScale)) + Center;
                r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center, Matrix4x4.CreateScale(Vector3.One / FakeScale)) + Center;
                r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center, Matrix4x4.CreateScale(Vector3.One / FakeScale)) + Center;
            }
            else
            {
                r.RailPoint.Point0Trans = Vector3.Transform(r.RailPoint.Point0Trans - Center, Matrix4x4.CreateScale(Vector3.One / 0.01f)) + Center;
                r.RailPoint.Point1Trans = Vector3.Transform(r.RailPoint.Point1Trans - Center, Matrix4x4.CreateScale(Vector3.One / 0.01f)) + Center;
                r.RailPoint.Point2Trans = Vector3.Transform(r.RailPoint.Point2Trans - Center, Matrix4x4.CreateScale(Vector3.One / 0.01f)) + Center;
            }
        }
        // Modify the points directly during scale
    }
}
