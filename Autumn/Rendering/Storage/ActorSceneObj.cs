using System.Numerics;
using Autumn.Background;
using Autumn.FileSystems;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;

namespace Autumn.Rendering.Storage;

internal class ActorSceneObj : IStageSceneObj
{
    public StageObj StageObj { get; }
    public Actor Actor { get; set; }

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;

    public ActorSceneObj(StageObj stageObj, Actor actorObj, uint pickingId)
    {
        StageObj = stageObj;
        Actor = actorObj;
        PickingId = pickingId;

        AABB = actorObj.AABB;

        UpdateTransform();
    }

    public void UpdateTransform()
    {
        Vector3 scale = Actor.IsEmptyModel ? StageObj.Scale : StageObj.Scale * 0.01f;
        Transform = MathUtils.CreateTransform(StageObj.Translation * 0.01f, scale, StageObj.Rotation);
    }

    public void UpdateActor(LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        string actorName = StageObj.Name;

        if (
            StageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        fsHandler.ReadCreatorClassNameTable().TryGetValue(actorName, out string? fallback);

        if (fallback != null && ClassDatabaseWrapper.DatabaseEntries.ContainsKey(fallback) && ClassDatabaseWrapper.DatabaseEntries[fallback].ArchiveName != null)
        {
            Actor = fsHandler.ReadActor(actorName, ClassDatabaseWrapper.DatabaseEntries[fallback].ArchiveName, fallback, scheduler);
        }
        else if (ClassDatabaseWrapper.DatabaseEntries.ContainsKey(actorName) && ClassDatabaseWrapper.DatabaseEntries[actorName].ArchiveName != null)
            Actor = fsHandler.ReadActor(actorName, ClassDatabaseWrapper.DatabaseEntries[actorName].ArchiveName, scheduler);
        else
            Actor = fsHandler.ReadActor(actorName, fallback, scheduler);

        UpdateTransform();
    }
}
