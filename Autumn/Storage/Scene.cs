using AutumnStageEditor.Storage.StageObj.Interfaces;
using SceneGL;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AutumnSceneGL.Storage {
    internal class Scene {
        //public readonly List<Instances.InstanceData> InstanceList = new();
        public Stage Stage { get; set; }
        public readonly List<SceneObj> SceneObjects = new();

        public Scene(Stage stage) {
            Stage = stage;

            stage.StageObjs.ForEach((obj) => SceneObjects.Add(new(obj)));

            //foreach(IBaseObj obj in stage.StageObjs) {
            //    Matrix4x4 transform =
            //        Matrix4x4.CreateTranslation(obj.Translation) *
            //        Matrix4x4.CreateScale(obj.Scale) *
            //        Matrix4x4.CreateRotationX(0);

            //    InstanceList.Add(new() { Transform = transform });
            //}
        }

        public void Render(GL gl, Material mat, Matrix4x4 viewProjection) {
            //Instances.Render(gl, mat, viewProjection, CollectionsMarshal.AsSpan(InstanceList));
        }
    }
}
