using System.Numerics;

namespace Autumn.Rendering.Storage;

internal interface ISceneObj
{
    public Matrix4x4 Transform { get; set; }

    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; }

    public void UpdateTransform();
}
