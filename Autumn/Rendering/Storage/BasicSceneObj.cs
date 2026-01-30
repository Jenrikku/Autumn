using System.Numerics;
using Autumn.Storage;
using Autumn.Utils;

namespace Autumn.Rendering.Storage;

internal class BasicSceneObj : IStageSceneObj
{
    public StageObj StageObj { get; }
    public CommonMaterialParameters MaterialParams { get; }

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public BasicSceneObj(StageObj stageObj, CommonMaterialParameters matParams, float aabbMult, uint pickingId)
    {
        StageObj = stageObj;
        MaterialParams = matParams;
        PickingId = pickingId;

        AABB = new(aabbMult);

        UpdateTransform();
    }

    public void UpdateTransform() =>
        Transform = MathUtils.CreateTransform(StageObj.Translation * 0.01f, StageObj.Scale, StageObj.Rotation);
}
