using Autumn.IO;
using Autumn.Storage;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Autumn.Scene;

internal class Scene
{
    public Stage Stage { get; set; }
    public List<SceneObj> SceneObjects { get; } = new();
    public List<SceneObj> SelectedObjects { get; } = new();

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

    /// <returns>Whether the object is now selected. It will be false as well whenever no object was found.</returns>
    public bool ToggleObjectSelection(uint id)
    {
        if (!_pickableObjs.TryGetValue(id, out SceneObj? sceneObj))
            return false;

        sceneObj.Selected |= true;

        if (sceneObj.Selected)
            SelectedObjects.Add(sceneObj);

        return sceneObj.Selected;
    }

    public void UnselectAllObjects()
    {
        foreach (SceneObj sceneObj in SelectedObjects)
            sceneObj.Selected = false;

        SelectedObjects.Clear();
    }

    public void GenerateSceneObjects(ref string status)
    {
        SceneObjects.Clear();
        SelectedObjects.Clear();

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
            ActorObj actorObj = ObjectHandler.GetObject(stageObj.Properties.TryGetValue("ModelName", out object? result) ? (string)result : stageObj.Name);

            SceneObj sceneObj = new(stageObj, actorObj, _lastPickingId);

            SceneObjects.Add(sceneObj);
            _pickableObjs.Add(_lastPickingId++, sceneObj);

            GenerateSceneObjects(stageObj.Children, ref status);
            curObj++;
        }

        status = string.Empty;
    }
}
