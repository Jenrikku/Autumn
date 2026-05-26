using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.Materials;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class TransparentWallRenderer
{
    private struct Vertex
    {
        [VertexAttribute(CombinerMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, false)]
        public Vector3 Position;
        [VertexAttribute(AttributeShaderLoc.Loc1, 2, VertexAttribPointerType.Float, false)]
        public Vector2 UV;
    }
    private static RenderableModel? s_WallModel;

    public static void Initialize(GL gl) => s_WallModel = GenerateModel(gl);
    
    public static RenderableModel GenerateModel(GL gl, float scale = 10)
    {
        ModelBuilder<ushort, Vertex> builder = new();

        Matrix4x4 mtx;
        scale *= 100;
        #region Transform Helpers
        void Reset() => mtx = Matrix4x4.CreateScale(scale);

        static void Rotate(ref float x, ref float y)
        {
            var _x = x;
            x = y;
            y = -_x;
        }

        void RotateOnX()
        {
            Rotate(ref mtx.M12, ref mtx.M13);
            Rotate(ref mtx.M22, ref mtx.M23);
            Rotate(ref mtx.M32, ref mtx.M33);
        }

        void RotateOnY()
        {
            Rotate(ref mtx.M11, ref mtx.M13);
            Rotate(ref mtx.M21, ref mtx.M23);
            Rotate(ref mtx.M31, ref mtx.M33);
        }

        #endregion

        float w = 1;
        
        void Face()
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 0, -w), mtx), UV = Vector2.Zero },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 0, -w), mtx), UV = Vector2.UnitX },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 0, w), mtx), UV = Vector2.UnitY },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 0, w), mtx), UV = Vector2.One }
            );
        }
        Reset();

        RotateOnX();
        Face();

        Reset();


        return builder.GetModel(gl);
    }

    public static void Render(GL gl, CommonSceneParameters scene, CommonMaterialParameters material, uint pickingId)
    {
        if (!TransparentWallMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.Disable(EnableCap.CullFace);
            int location = -1;
            if (TransparentWallMaterial.WallProgram.TryGetUniformLoc("uPickingId", out location))
                gl.Uniform1(location, pickingId);
            s_WallModel!.Draw(gl);

            gl.Enable(EnableCap.CullFace);
        }
    }

    public static void CleanUp(GL gl) => s_WallModel?.CleanUp(gl);
    
}
