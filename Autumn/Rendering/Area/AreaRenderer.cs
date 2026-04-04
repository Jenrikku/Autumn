using System.Numerics;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.Materials;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Area;

internal static class AreaRenderer
{
    private struct Vertex
    {
        [VertexAttribute(CombinerMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, false)]
        public Vector3 Position;
        [VertexAttribute(AttributeShaderLoc.Loc1, 2, VertexAttribPointerType.Float, false)]
        public Vector2 UV;
        [VertexAttribute(AttributeShaderLoc.Loc2, 1, VertexAttribPointerType.Float, false)]
        public float FaceType; // 0 -> Side, 1 -> Top, 2 -> Bottom
    }
    private static RenderableModel? s_CubeModel;
    private static RenderableModel? s_SphereModel;
    private static RenderableModel? s_CylinderModel;

    public static void Initialize(GL gl)
    {
        s_CubeModel = GenerateCubeModel(gl);
        s_SphereModel = GenerateSphereModel(gl);
        s_CylinderModel = GenerateCylinderModel(gl);
    }
    public static RenderableModel GenerateCubeModel(GL gl, float scale = 10)
    {
        ModelBuilder<ushort, Vertex> builder = new();

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

        #endregion

        float w = 1;
        
        #region Cube part Helpers
        void Face(uint facetype)
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, -w), mtx), UV = Vector2.Zero, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -w), mtx), UV = Vector2.UnitX, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, w), mtx), UV = Vector2.UnitY, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, w), mtx), UV = Vector2.One, FaceType = facetype }
            );
        }
        #endregion


        #region Construction

        Reset();

        #region Faces
        Face(1);
        RotateOnX();

        for (int i = 0; i < 4; i++)
        {
            Face(0);
            RotateOnY();
        }
        RotateOnX();
        Face(2);
        #endregion

        Reset();


        #endregion


        return builder.GetModel(gl);
    }
    public static RenderableModel GenerateSphereModel(GL gl, float scale = 10)
    {
        ModelBuilder<ushort, Vertex> builder = new();
        scale /= 1.5f;
        #region Transform Helpers

        #endregion

        int vertical_slices = 8;
        int horizontal_slices = 8;
        float horizontal_steps = MathF.PI / horizontal_slices;
        float vertical_steps = MathF.Tau / vertical_slices;

        List<Vector3> vertexPositions = new();
        vertexPositions.Add(new Vector3(0, scale, 0));
        for (int i = 1; i < (horizontal_slices); i++)
        {
            float horizontal = i * horizontal_steps;
            for (int j = 0; j < vertical_slices; ++j)
            {
                float vertical = j * vertical_steps;
                vertexPositions.Add(
                    new Vector3(scale * MathF.Sin(horizontal) * MathF.Cos(vertical),
                                                        scale * MathF.Cos(horizontal),
                                                        -scale * MathF.Sin(horizontal) * MathF.Sin(vertical)));

            }
        }
        vertexPositions.Add(new Vector3(0, -scale, 0));

        for (int i = 1; i <= vertical_slices; i++)
        {
            if (i < vertical_slices)
                builder!.AddTriangle(new Vertex { Position = vertexPositions[0], FaceType = 0, UV = Vector2.Zero },
                                       new Vertex { Position = vertexPositions[i], FaceType = 0, UV = Vector2.Zero },
                                       new Vertex { Position = vertexPositions[i + 1], FaceType = 0, UV = Vector2.Zero });
            else
                builder!.AddTriangle(new Vertex { Position = vertexPositions[0], FaceType = 0, UV = Vector2.Zero },
                                       new Vertex { Position = vertexPositions[i], FaceType = 0, UV = Vector2.Zero },
                                       new Vertex { Position = vertexPositions[1], FaceType = 0, UV = Vector2.Zero });
        }

        for (int h_sl = 0; h_sl < horizontal_slices - 2; h_sl++)
        {
            for (int v_sl = 0; v_sl < vertical_slices - 1; v_sl++)
            {
                if (v_sl < vertical_slices - 1)
                    builder.AddPlane(new Vertex { Position = vertexPositions[1 + v_sl + h_sl * vertical_slices], FaceType = 0, UV = Vector2.Zero },
                                       new Vertex { Position = vertexPositions[1 + (v_sl + 1) + h_sl * vertical_slices], FaceType = 0, UV = Vector2.UnitX },
                                       new Vertex { Position = vertexPositions[1 + v_sl + (h_sl + 1) * vertical_slices], FaceType = 0, UV = Vector2.UnitY },
                                       new Vertex { Position = vertexPositions[1 + (v_sl + 1) + (h_sl + 1) * vertical_slices], FaceType = 0, UV = Vector2.One });
            }

            builder.AddPlane(new Vertex { Position = vertexPositions[vertical_slices + h_sl * vertical_slices], FaceType = 0, UV = Vector2.Zero },
                                new Vertex { Position = vertexPositions[1 + h_sl * vertical_slices], FaceType = 0, UV = Vector2.UnitX },
                                new Vertex { Position = vertexPositions[vertical_slices + (h_sl + 1) * vertical_slices], FaceType = 0, UV = Vector2.UnitY },
                                new Vertex { Position = vertexPositions[1 + (h_sl + 1) * vertical_slices], FaceType = 0, UV = Vector2.One });
        }

        return builder.GetModel(gl);
    }
    public static RenderableModel GenerateCylinderModel(GL gl, float scale = 5)
    {
        ModelBuilder<ushort, Vertex> builder = new();
        Matrix4x4 mtx;
        #region Transform Helpers
        void Reset() => mtx = Matrix4x4.CreateScale(scale);

        float sides = 20;
        float height = scale / 1.65f;
        float radius = scale / 1.7f;
        scale /= 1.5f;
        float sideLength = 2 * radius * MathF.Sin(MathF.PI / sides);

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

        #endregion

        #region aaa
        void Side(uint facetype)
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(0, radius,  height), mtx), UV = Vector2.Zero, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(0, radius,  0), mtx), UV = Vector2.UnitX, FaceType = facetype },

                new Vertex { Position = Vector3.Transform(new Vector3(0, radius,  height), mtx * Matrix4x4.CreateRotationY(MathF.PI / sides * 2)), UV = Vector2.UnitY, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(0, radius,  0), mtx * Matrix4x4.CreateRotationY(MathF.PI / sides * 2)), UV = Vector2.One, FaceType = facetype }
            );
        }
        void Face(uint facetype)
        {
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-radius, facetype == 2 ? height : 0, -radius), mtx), UV = Vector2.Zero, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3( radius, facetype == 2 ? height : 0, -radius), mtx), UV = Vector2.UnitX, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3(-radius, facetype == 2 ? height : 0,  radius), mtx), UV = Vector2.UnitY, FaceType = facetype },
                new Vertex { Position = Vector3.Transform(new Vector3( radius, facetype == 2 ? height : 0,  radius), mtx), UV = Vector2.One, FaceType = facetype }
            );
        }
        #endregion


        #region Construction

        Reset();

        #region Faces
        // Top
        Face(1);
        for (int i = 0; i < sides; i++)
        {
            Reset();
            mtx *= Matrix4x4.CreateRotationX(0.5f * MathF.PI);
            mtx *= Matrix4x4.CreateRotationY(MathF.PI / sides * i * 2);
            Side(0);
        }
        Reset();
        RotateOnX();
        RotateOnX();
        // // Bottom
        Face(2);
        #endregion

        #endregion


        return builder.GetModel(gl);
    }

    public static void Render(GL gl, CommonSceneParameters scene, CommonMaterialParameters material, int areaType, uint pickingId)
    {
        if (!AreaMaterial.TryUse(gl, areaType, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.Disable(EnableCap.CullFace);
            int location = -1;
            switch (areaType)
            {
                case 1:
                    if (AreaMaterial.CenterCubeProgram.TryGetUniformLoc("uPickingId", out location))
                        gl.Uniform1(location, pickingId);
                    s_CubeModel!.Draw(gl);
                    break;
                case 2:
                    if (AreaMaterial.SphereProgram.TryGetUniformLoc("uPickingId", out location))
                        gl.Uniform1(location, pickingId);
                    s_SphereModel!.Draw(gl);
                    break;
                case 3:
                    if (AreaMaterial.CylinderProgram.TryGetUniformLoc("uPickingId", out location))
                        gl.Uniform1(location, pickingId);
                    s_CylinderModel!.Draw(gl);
                    break;
                default:
                    if (AreaMaterial.BaseCubeProgram.TryGetUniformLoc("uPickingId", out location))
                        gl.Uniform1(location, pickingId);
                    s_CubeModel!.Draw(gl);
                    break;
            }

            gl.Enable(EnableCap.CullFace);
        }
    }

    public static void CleanUp(GL gl) 
    {
        s_CubeModel?.CleanUp(gl);
        s_SphereModel?.CleanUp(gl);
        s_CylinderModel?.CleanUp(gl);
    }
}
