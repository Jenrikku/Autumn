using System.Numerics;

namespace Autumn.Rendering.Storage;

internal class RailHandleSceneObj : ISceneObj
{
    private readonly Action? _railUpdate;

    public Vector3 Translation = new();

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public RailHandleSceneObj(Vector3 translation, Action railUpdate, ref uint pickingId)
    {
        Translation = translation;
        AABB = new(1f); // TO-DO
        PickingId = pickingId++;

        UpdateTransform();

        _railUpdate = railUpdate; // Important: Putting it at the end prevents it from being called when constructing
    }

    public void UpdateTransform()
    {
        Transform = Matrix4x4.CreateTranslation(Translation * 0.01f);
        _railUpdate?.Invoke();
    }
}
