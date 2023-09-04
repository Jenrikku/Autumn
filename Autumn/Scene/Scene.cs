using Autumn.IO;
using Autumn.Storage;
using SceneGL;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Autumn.Scene;

internal class Scene
{
    public Stage Stage { get; set; }
    public readonly List<SceneObj> SceneObjects = new();

    public Scene(Stage stage)
    {
        Stage = stage;

        stage.StageData.ForEach(
            (stageObj) =>
            {
                ActorObj actorObj = ObjectHandler.GetObject(stageObj.Name);

                SceneObj sceneObj = new(stageObj, actorObj);

                SceneObjects.Add(sceneObj);
            }
        );
    }

    public void Render(GL gl, in Matrix4x4 view, in Matrix4x4 projection)
    {
        ModelRenderer.UpdateMatrices(view, projection);

        foreach (SceneObj obj in SceneObjects)
        {
            //#if DEBUG
            //            if (obj.StageObj.Name == "FirstStepASideView")
            //            {
            //                obj.Transform = Matrix4x4.Identity;
            //
            //                ModelRenderer.Draw(gl, obj);
            //            }
            //#else
            ModelRenderer.Draw(gl, obj);
            //#endif
        }
    }
}
