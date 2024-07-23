using System.Numerics;
using Autumn.Enums;
using Autumn.History;
using Autumn.IO;
using Autumn.Storage;
using Silk.NET.OpenGL;

namespace Autumn.Scene;

internal class Scene
{
    public Stage Stage { get; set; }
    public List<SceneObj> SceneObjects { get; } = new();

    public ChangeHistory History { get; } = new();

    private readonly List<SceneObj> _selectedObjects = new();
    public IEnumerable<SceneObj> SelectedObjects => _selectedObjects;

    public Camera Camera { get; } = new(new Vector3(-10, 7, 10), Vector3.Zero);

    /// <summary>
    /// Specifies whether the scene is ready to be rendered.
    /// </summary>
    public bool IsReady { get; set; } = false;

    private uint _lastPickingId = 0;
    private readonly Dictionary<uint, SceneObj> _pickableObjs = new();

    public Scene(Stage stage) => Stage = stage;

    public void Render(GL gl, in Matrix4x4 view, in Matrix4x4 projection)
    {
        ModelRenderer.UpdateMatrices(view, projection);

        foreach (SceneObj obj in SceneObjects)
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

    public void GenerateSceneObjects(ref string status)
    {
        SceneObjects.Clear();
        _selectedObjects.Clear();

        GenerateSceneObjects(Stage.StageData, ref status);

        IsReady = true;
    }

    private void GenerateSceneObjects(List<StageObj>? stageData, ref string status)
    {
        if (stageData is null)
            return;

        int curObj = 0;
        foreach (StageObj stageObj in stageData)
        {
            status = $"{curObj}/{stageData.Count} Loading model for {stageObj.Name}";

            GenerateSceneObject(stageObj);
            GenerateSceneObjects(stageObj.Children, ref status);
            curObj++;
        }

        status = string.Empty;
    }

    public void GenerateSceneObject(StageObj stageObj)
    {
        string actorName = stageObj.Name;

        if (
            stageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        ActorObj actorObj = ObjectHandler.GetObject(actorName);
        SceneObj sceneObj = new(stageObj, actorObj, _lastPickingId);

        SceneObjects.Add(sceneObj);
        _pickableObjs.Add(_lastPickingId++, sceneObj);
    }

    public void ResetCamera()
    {
        // Find the first Mario and set the camera to its position.
        StageObj? mario = SceneObjects
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
}
