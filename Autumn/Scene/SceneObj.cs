using Autumn.Storage;
using Autumn.Utils;
using System.Numerics;

namespace Autumn.Scene;

internal class SceneObj
{
    public StageObj StageObj { get; }
    public ActorObj ActorObj { get; }

    public Matrix4x4 Transform;

    public uint PickingId { get; private set; }
    public bool Selected { get; set; }

    public SceneObj(StageObj stageObj, ActorObj actorObj, uint pickingId)
    {
        StageObj = stageObj;
        ActorObj = actorObj;
        PickingId = pickingId;

        UpdateTransform();
    }

    public void UpdateTransform() =>
        Transform = MathUtils.CreateTransform(
            StageObj.Translation * 0.01f,
            StageObj.Scale,
            StageObj.Rotation
        );
}
