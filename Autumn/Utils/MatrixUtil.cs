using Silk.NET.Maths;
using System.Numerics;

namespace AutumnSceneGL.Utils {
    internal static class MatrixUtil {
        // Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/Util/NumericsUtil.cs
        /// <summary>
        /// Creates a perspective projection matrix based on a field of view, aspect ratio, and near and far view plane distances. 
        /// </summary>
        /// <param name="fieldOfView">Field of view in the y direction, in radians.</param>
        /// <param name="aspectRatio">Aspect ratio, defined as view space width divided by height.</param>
        /// <param name="nearPlaneDistance">Distance to the near view plane.</param>
        /// <param name="farPlaneDistance">Distance to the far view plane.</param>
        /// <returns>The perspective projection matrix.</returns>
        public static Matrix4x4 CreatePerspectiveReversedDepth(float fieldOfView, float aspectRatio, float nearPlaneDistance) {
            if(fieldOfView <= 0.0f || fieldOfView >= Math.PI)
                throw new ArgumentOutOfRangeException("fieldOfView");

            if(nearPlaneDistance <= 0.0f)
                throw new ArgumentOutOfRangeException("nearPlaneDistance");

            float yScale = 1.0f / (float) Math.Tan(fieldOfView * 0.5f);
            float xScale = yScale / aspectRatio;

            Matrix4x4 result;

            result.M11 = xScale;
            result.M12 = result.M13 = result.M14 = 0.0f;

            result.M22 = yScale;
            result.M21 = result.M23 = result.M24 = 0.0f;

            result.M31 = result.M32 = result.M33 = 0.0f;
            result.M34 = -1.0f;

            result.M41 = result.M42 = result.M44 = 0.0f;
            result.M43 = nearPlaneDistance;

            return result;
        }

        public static Matrix3X4<float> CreateTransform(Vector3 translation, Vector3 scale, Vector3 rotation) {
            double rotX = Math.PI / 180 * rotation.X,
                   rotY = Math.PI / 180 * rotation.Y,
                   rotZ = Math.PI / 180 * rotation.Z;

            // M = T * S * R
            // R = Rx * Ry * Rz
            // Where T -> Translation, S -> Scale, R -> Rotation.

            return new() {
                M11 = (float) (scale.X * (Math.Cos(rotY) * Math.Cos(rotZ))),
                M12 = (float) (scale.X * (Math.Cos(rotY) * Math.Sin(rotZ))),
                M13 = (float) (scale.X * -Math.Sin(rotY)),
                M14 = translation.X,

                M21 = (float) (scale.Y * (Math.Sin(rotX) * Math.Sin(rotY) * Math.Cos(rotZ) - Math.Cos(rotX) * Math.Sin(rotZ))),
                M22 = (float) (scale.Y * (Math.Sin(rotX) * Math.Sin(rotY) * Math.Sin(rotZ) + Math.Cos(rotX) * Math.Cos(rotZ))),
                M23 = (float) (scale.Y * (Math.Sin(rotX) * Math.Cos(rotY))),
                M24 = translation.Y,

                M31 = (float) (scale.Z * (Math.Cos(rotX) * Math.Sin(rotY) * Math.Cos(rotZ) - Math.Sin(rotX) * Math.Sin(rotZ))),
                M32 = (float) (scale.Z * (Math.Cos(rotX) * Math.Sin(rotY) * Math.Sin(rotZ) - Math.Sin(rotX) * Math.Cos(rotZ))),
                M33 = (float) (scale.Z * (Math.Cos(rotX) * Math.Cos(rotY))),
                M34 = translation.Z
            };
        }
    }
}
