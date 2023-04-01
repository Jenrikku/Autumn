using AutumnSceneGL.Utils;
using AutumnStageEditor.Storage.StageObj.Interfaces;
using Silk.NET.Maths;

namespace AutumnSceneGL.Storage {
    internal class SceneObj {
        public IBaseObj StageObj { get; }
        public Matrix3X4<float> TransformData;

        // TO-DO: Add rendering members.

        public SceneObj(IBaseObj stageObj) {
            StageObj = stageObj;

            UpdateTransform();
        }

        public void UpdateTransform() =>
            TransformData = MatrixUtil.CreateTransform(
                StageObj.Translation,
                StageObj.Scale,
                StageObj.Rotation);
    }
}
