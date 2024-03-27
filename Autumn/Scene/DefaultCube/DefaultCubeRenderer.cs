using System.Numerics;
using SceneGL;
using SceneGL.Materials;
using Silk.NET.OpenGL;

namespace Autumn.Scene.DefaultCube;

internal static class DefaultCubeRenderer
{
    private struct Vertex
    {
        [VertexAttribute(CombinerMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, false)]
        public Vector3 Position;
    }

    private static RenderableModel? s_model;

    public static void Initialize(GL gl) => s_model = GenerateCubeModel(gl);

    public static RenderableModel GenerateCubeModel(GL gl, float scale = 0.5f)
    {
        ModelBuilder<ushort, Vertex> builder = new();

        float BEVEL = 0.1f;

        Matrix4x4 mtx;

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

        void RotateOnZ()
        {
            Rotate(ref mtx.M11, ref mtx.M12);
            Rotate(ref mtx.M21, ref mtx.M22);
            Rotate(ref mtx.M31, ref mtx.M32);
        }

        #endregion

        float w = 1 - BEVEL;
        float m = 1;

        #region Cube part Helpers
        void Face()
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, -w), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -w), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, w), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, w), mtx) }
            );
        }

        void Bevel()
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, w), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, w), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, m, m), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, m, m), mtx) }
            );

            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, m, m), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, m, m), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, w, 1), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, w, 1), mtx) }
            );
        }

        void BevelCorner()
        {
            void Piece(Vector3 v1, Vector3 v2, Vector3 v3)
            {
                Vector3 vm = new(m, m, m);

                builder!.AddTriangle(
                    new Vertex { Position = Vector3.Transform(v1, mtx) },
                    new Vertex { Position = Vector3.Transform(vm, mtx) },
                    new Vertex { Position = Vector3.Transform(v2, mtx) }
                );

                builder!.AddTriangle(
                    new Vertex { Position = Vector3.Transform(v2, mtx) },
                    new Vertex { Position = Vector3.Transform(vm, mtx) },
                    new Vertex { Position = Vector3.Transform(v3, mtx) }
                );
            }

            Piece(new Vector3(w, w, 1), new Vector3(w, m, m), new Vector3(w, 1, w));
            Piece(new Vector3(w, 1, w), new Vector3(m, m, w), new Vector3(1, w, w));
            Piece(new Vector3(1, w, w), new Vector3(m, w, m), new Vector3(w, w, 1));
        }
        #endregion


        #region Construction

        Reset();

        #region Faces
        Face();
        RotateOnX();

        for (int i = 0; i < 4; i++)
        {
            Face();
            RotateOnY();
        }
        RotateOnX();
        Face();
        #endregion

        Reset();

        #region Edges/Bevels
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Bevel();
                RotateOnY();
            }

            RotateOnZ();
        }
        #endregion

        Reset();

        #region Corners/BevelCorners
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                BevelCorner();
                RotateOnY();
            }

            RotateOnZ();
        }
        #endregion

        #endregion


        return builder.GetModel(gl);
    }

    public static void Render(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        uint pickingId
    )
    {
        if (!DefaultCubeMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.CullFace(TriangleFace.Back);

            if (DefaultCubeMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
                gl.Uniform1(location, pickingId);

            s_model!.Draw(gl);
        }
    }

    public static void CleanUp(GL gl)
    {
        s_model?.CleanUp(gl);
    }
}
