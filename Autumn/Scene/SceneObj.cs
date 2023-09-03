using Autumn.Storage;
using Autumn.Storage.StageObjs;
using Autumn.Utils;
using System.Numerics;

namespace Autumn.Scene;

internal class SceneObj
{
    public IStageObj StageObj { get; }
    public ActorObj ActorObj { get; }

    public Matrix4x4 Transform;

    public bool Selected { get; set; }

    public SceneObj(IStageObj stageObj, ActorObj actorObj)
    {
        StageObj = stageObj;
        ActorObj = actorObj;

        UpdateTransform();
    }

    public void UpdateTransform() =>
        Transform = MathUtils.CreateTransform(
            StageObj.Translation * 0.01f,
            StageObj.Scale,
            StageObj.Rotation
        );
}
