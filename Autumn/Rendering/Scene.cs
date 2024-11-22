using System.Numerics;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.History;
using Autumn.Storage;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal class Scene
{
    public Stage Stage { get; }

    public bool IsSaved { get; set; } = false;

    public ChangeHistory History { get; } = new();

    private readonly List<SceneObj> _selectedObjects = new();
    public IEnumerable<SceneObj> SelectedObjects => _selectedObjects;

    public Camera Camera { get; } = new(new Vector3(-10, 7, 10), Vector3.Zero);

    private readonly List<SceneObj> _sceneObjects = new();

    private uint _lastPickingId = 0;
    private readonly Dictionary<uint, SceneObj> _pickableObjs = new();

    public Scene(Stage stage, LayeredFSHandler fsHandler, GL gl, ref string status)
    {
        Stage = stage;
        GenerateSceneObjects(fsHandler, gl, ref status);
    }

    public void Render(GL gl, in Matrix4x4 view, in Matrix4x4 projection)
    {
        ModelRenderer.UpdateMatrices(view, projection);

        foreach (SceneObj obj in _sceneObjects)
            ModelRenderer.Draw(gl, obj);
    }

    public bool IsObjectSelected(uint id)
    {
        _pickableObjs.TryGetValue(id, out SceneObj? sceneObj);
        return sceneObj?.Selected ?? false;
    }

    public void SetObjectSelected(uint id, bool value)
    {
        if (!_pickableObjs.TryGetValue(id, out SceneObj? sceneObj))
            return;

        sceneObj.Selected = value;

        if (sceneObj.Selected)
            _selectedObjects.Add(sceneObj);
        else
            _selectedObjects.Remove(sceneObj);
    }

    public void UnselectAllObjects()
    {
        foreach (SceneObj sceneObj in _selectedObjects)
            sceneObj.Selected = false;

        _selectedObjects.Clear();
    }

    public void SetSelectedObjects(IEnumerable<SceneObj> objs)
    {
        UnselectAllObjects();

        foreach (SceneObj sceneObj in objs)
        {
            sceneObj.Selected = true;
            _selectedObjects.Add(sceneObj);
        }
    }

    public IEnumerable<SceneObj> EnumerateSceneObjs()
    {
        foreach (SceneObj sceneObj in _sceneObjects)
            yield return sceneObj;
    }

    public void AddObject(StageObj stageObj, LayeredFSHandler layeredFS, GL gl)
    {
        Stage.AddStageObj(stageObj);
        GenerateSceneObject(stageObj, layeredFS, gl);
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

    private void GenerateSceneObjects(LayeredFSHandler fsHandler, GL gl, ref string status)
    {
        _sceneObjects.Clear();
        _selectedObjects.Clear();

        if (Stage is null)
            return;

        foreach (StageObjType objType in Enum.GetValues<StageObjType>())
            GenerateSceneObjects(Stage.EnumerateStageObjs(objType), fsHandler, gl, ref status);
    }

    private void GenerateSceneObjects(
        IEnumerable<StageObj>? stageObjs,
        LayeredFSHandler fsHandler,
        GL gl,
        ref string status
    )
    {
        if (stageObjs is null)
            return;

        int curObj = 0;
        foreach (StageObj stageObj in stageObjs)
        {
            status = $"Loading model for {stageObj.Name}";

            GenerateSceneObject(stageObj, fsHandler, gl);
            GenerateSceneObjects(stageObj.Children, fsHandler, gl, ref status);
            ++curObj;
        }

        status = string.Empty;
    }

    private void GenerateSceneObject(StageObj stageObj, LayeredFSHandler fsHandler, GL gl)
    {
        string actorName = stageObj.Name;

        if (
            stageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        Actor actor = fsHandler.ReadActor(actorName, gl);
        SceneObj sceneObj = new(stageObj, actor, _lastPickingId);

        _sceneObjects.Add(sceneObj);
        _pickableObjs.Add(_lastPickingId++, sceneObj);
    }
}
