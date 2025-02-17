using System.Numerics;
using Autumn.Background;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.History;
using Autumn.Rendering.Area;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal class Scene
{
    public Stage Stage { get; }

    public bool IsSaved { get; set; } = false;
    public uint SaveUndoCount { get; set; } = 0;

    public ChangeHistory History { get; } = new();

    private readonly List<ISceneObj> _selectedObjects = new();
    public IEnumerable<ISceneObj> SelectedObjects => _selectedObjects;

    public Camera Camera { get; } = new(new Vector3(-10, 7, 10), Vector3.Zero);

    /// <summary>
    /// Specifies whether the scene is ready to be rendered.
    /// </summary>
    public bool IsReady { get; set; } = false;

    private readonly List<ISceneObj> _sceneObjects = new();

    private uint _lastPickingId = 0;
    private readonly Dictionary<uint, ISceneObj> _pickableObjs = new();

    public Scene(Stage stage, LayeredFSHandler fsHandler, GLTaskScheduler scheduler, ref string status)
    {
        Stage = stage;
        GenerateSceneObjects(fsHandler, scheduler, ref status);
        scheduler.EnqueueGLTask(gl => IsReady = true);
    }

    public void Render(GL gl, in Matrix4x4 view, in Matrix4x4 projection)
    {
        ModelRenderer.UpdateMatrices(view, projection);

        foreach (ISceneObj obj in _sceneObjects)
            ModelRenderer.Draw(gl, obj);
    }

    #region Object selection

    public bool IsObjectSelected(uint id)
    {
        _pickableObjs.TryGetValue(id, out ISceneObj? sceneObj);
        return sceneObj?.Selected ?? false;
    }

    public void SetObjectSelected(uint id, bool value)
    {
        if (!_pickableObjs.TryGetValue(id, out ISceneObj? sceneObj))
            return;

        sceneObj.Selected = value;

        if (sceneObj.Selected)
            _selectedObjects.Add(sceneObj);
        else
            _selectedObjects.Remove(sceneObj);
    }

    public void UnselectAllObjects()
    {
        foreach (ISceneObj sceneObj in _selectedObjects)
            sceneObj.Selected = false;

        _selectedObjects.Clear();
    }

    public void SetSelectedObjects(IEnumerable<ISceneObj> objs)
    {
        UnselectAllObjects();

        foreach (ISceneObj sceneObj in objs)
        {
            sceneObj.Selected = true;
            _selectedObjects.Add(sceneObj);
        }
    }

    public IEnumerable<ISceneObj> EnumerateSceneObjs()
    {
        foreach (ISceneObj sceneObj in _sceneObjects)
            yield return sceneObj;
    }

    public int CountSceneObjs()
    {
        return _sceneObjects.Count;
    }

    #endregion

    public void AddObject(StageObj stageObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        Stage.AddStageObj(stageObj);
        GenerateSceneObject(stageObj, fsHandler, scheduler);
    }

    public uint DuplicateObj(StageObj clonedObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        uint pickingId = 0;

        if (clonedObj.Type == StageObjType.Child || clonedObj.Type == StageObjType.AreaChild)
        {
            clonedObj.Parent!.Children!.Add(clonedObj);
            DuplicateChild(clonedObj, fsHandler, scheduler);
            return pickingId;
        }

        Stage.AddStageObj(clonedObj);
        GenerateSceneObject(clonedObj, fsHandler, scheduler);
        pickingId = _lastPickingId - 1;

        if (clonedObj.Children != null && clonedObj.Children.Count > 0)
        {
            foreach (StageObj ch in clonedObj.Children)
            {
                pickingId = DuplicateChild(ch, fsHandler, scheduler);
            }
        }

        return pickingId;
    }

    public uint DuplicateChild(StageObj clonedObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        GenerateSceneObject(clonedObj, fsHandler, scheduler);
        uint pickingId = _lastPickingId - 1;
        if (clonedObj.Children != null && clonedObj.Children.Count > 0)
        {
            foreach (StageObj ch in clonedObj.Children)
            {
                pickingId = DuplicateChild(ch, fsHandler, scheduler);
            }
        }
        return pickingId;
    }

    public void RemoveObject(ISceneObj sceneObj)
    {
        Stage.RemoveStageObj(sceneObj.StageObj);
        DestroySceneObject(sceneObj);
    }

    public void ResetCamera()
    {
        // Find the first Mario and set the camera to its position.
        StageObj? mario = _sceneObjects
            .Find(
                (sceneObj) =>
                    sceneObj.StageObj.Type == StageObjType.Start
                    && sceneObj.StageObj.Properties.TryGetValue("MarioNo", out object? marioNoObj)
                    && marioNoObj is int marioNo
                    && marioNo == 0
            )
            ?.StageObj;

        if (mario is null)
        {
            Camera.LookAt(new Vector3(-10, 7, 10), Vector3.Zero);
            return;
        }

        float rotX = (float)(mario.Rotation.X * (Math.PI / 180f));
        float rotY = (float)(mario.Rotation.Y * (Math.PI / 180f));
        float rotZ = (float)(mario.Rotation.Z * (Math.PI / 180f));

        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(rotY, rotZ, rotX);
        rotation.X += 0.5f;
        rotation = Quaternion.Normalize(rotation);

        Vector3 cameraDistance = Vector3.Transform(Vector3.UnitZ, rotation) * 13;
        Vector3 marioPos = mario.Translation / 100f;
        Vector3 eye = marioPos - cameraDistance;

        Camera.LookAt(eye, marioPos);
    }

    private void GenerateSceneObjects(LayeredFSHandler fsHandler, GLTaskScheduler scheduler, ref string status)
    {
        _sceneObjects.Clear();
        _selectedObjects.Clear();

        if (Stage is null)
            return;

        IEnumerable<StageObjType> types = Enum.GetValues<StageObjType>()
            .Where(t => t != StageObjType.Rail && t != StageObjType.AreaChild && t != StageObjType.Child);

        foreach (StageObjType objType in types)
            GenerateSceneObjects(Stage.EnumerateStageObjs(objType), fsHandler, scheduler, ref status);
    }

    private void GenerateSceneObjects(
        IEnumerable<StageObj>? stageObjs,
        LayeredFSHandler fsHandler,
        GLTaskScheduler scheduler,
        ref string status
    )
    {
        if (stageObjs is null)
            return;

        foreach (StageObj stageObj in stageObjs)
        {
            status = $"Loading model for {stageObj.Name}";

            GenerateSceneObject(stageObj, fsHandler, scheduler);
            GenerateSceneObjects(stageObj.Children, fsHandler, scheduler, ref status);
        }

        status = string.Empty;
    }

    private void GenerateSceneObject(StageObj stageObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        // Areas:
        if (stageObj.IsArea())
        {
            Vector4 color = AreaMaterial.GetAreaColor(stageObj.Name);
            CommonMaterialParameters matParams = new(color, new());

            BasicSceneObj areaSceneObj = new(stageObj, matParams, 20f, _lastPickingId);
            _sceneObjects.Add(areaSceneObj);
            _pickableObjs.Add(_lastPickingId++, areaSceneObj);
            return;
        }

        // Rails:
        if (stageObj.Type == StageObjType.Rail && stageObj is RailObj rail)
        {
            RailSceneObj railSceneObj = new(rail, ref _lastPickingId);

            // TO-DO: Pickable rail and rail points.

            _sceneObjects.Add(railSceneObj);
            return;
        }

        // Actors:
        string actorName = stageObj.Name;

        if (
            stageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        fsHandler.ReadCreatorClassNameTable().TryGetValue(actorName, out string? fallback);
        Actor actor = fsHandler.ReadActor(actorName, fallback, scheduler);
        ActorSceneObj actorSceneObj = new(stageObj, actor, _lastPickingId);

        _sceneObjects.Add(actorSceneObj);
        _pickableObjs.Add(_lastPickingId++, actorSceneObj);
    }

    private void DestroySceneObject(ISceneObj sceneObj)
    {
        _sceneObjects.Remove(sceneObj);
        _pickableObjs.Remove(sceneObj.PickingId);
    }
}
