using System.Numerics;
using Autumn.Background;
using Autumn.FileSystems;
using Autumn.Storage;
using Autumn.Utils;

namespace Autumn.Rendering;

internal class SceneObj
{
    public StageObj StageObj { get; }
    public Actor Actor { get; }

    public Matrix4x4 Transform;

    public uint PickingId { get; private set; }
    public bool Selected { get; set; }

    public SceneObj(StageObj stageObj, Actor actorObj, uint pickingId)
    {
        StageObj = stageObj;
        Actor = actorObj;
        PickingId = pickingId;

        UpdateTransform();
    }

    public void UpdateTransform() =>
        Transform = MathUtils.CreateTransform(
            StageObj.Translation * 0.01f,
            StageObj.Scale,
            StageObj.Rotation
        );
    

    public void UpdateActor(
        LayeredFSHandler fsHandler,
        GLTaskScheduler scheduler
    )
    {
        string actorName = StageObj.Name;
        if (
            StageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        Actor = fsHandler.ReadActor(actorName, scheduler);
    }
    
}
