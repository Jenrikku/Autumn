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

    private uint _lastPickingId = 0;
    private readonly Dictionary<uint, SceneObj> _pickableObjs = new();

    public Scene(Stage stage)
    {
        Stage = stage;

        if (stage.StageObjs is null)
            return;

        foreach (StageObj stageObj in stage.StageObjs)
        {
            ActorObj actorObj = ObjectHandler.GetObject(stageObj.Name);

            SceneObj sceneObj = new(stageObj, actorObj, _lastPickingId);

            SceneObjects.Add(sceneObj);
            _pickableObjs.Add(_lastPickingId++, sceneObj);
        }
    }

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
}
