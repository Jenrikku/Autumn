using System.Numerics;
using Autumn.Rendering.Rail;
using Autumn.Storage;

namespace Autumn.Rendering.Storage;

internal class RailSceneObj : ISceneObj
{
    private readonly HashSet<uint> _assignedIds = new(); // Optimization, reduces CPU overhead when checking ids

    public RailObj RailObj { get; }
    public RailModel RailModel { get; }

    public List<RailPointSceneObj> RailPoints { get; } = new();

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

        AABB = new(1); // TO-DO

        foreach (RailPoint railPoint in rail.Points)
        {
            uint oldId = pickingId;

            RailPointSceneObj sceneObj = new(railPoint, railModel.UpdateModel, ref pickingId);
            RailPoints.Add(sceneObj);

            for (uint i = oldId; i < pickingId; i++)
                _assignedIds.Add(i);
        }
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

    public void UpdateTransform() { }
}
