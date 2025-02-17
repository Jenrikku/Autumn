using System.Numerics;
using Autumn.Storage;

namespace Autumn.Rendering.Storage;

internal interface ISceneObj
{
    public StageObj StageObj { get; }

    public Matrix4x4 Transform { get; set; }

    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool IsVisible { get; set; }

    public void UpdateTransform();
}
