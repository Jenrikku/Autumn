using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using SceneGL;
using SceneGL.Materials;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailRenderer
{
    private struct Vertex
    {
        [VertexAttribute(CombinerMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, false)]
        public Vector3 Position;
    }

    private static readonly Matrix4x4 s_railTranslate = Matrix4x4.CreateTranslation(new(0.01f));

    private static RenderableModel? s_pointModel;
    private static RenderableModel? s_pointHandleModel;

    public static void Initialize(GL gl)
    {
        float scale = 0.3f;

        s_pointModel = GenerateRailPointModel(gl, scale);
        s_pointHandleModel = GenerateRailPointModel(gl, scale / 2);
    }

    internal static RenderableModel GenerateRailPointModel(GL gl, float scale = 0.5f)
    {
        ModelBuilder<ushort, Vertex> builder = new();

        float bevel = 0.1f;

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


        float w = 1 - bevel;
        float h = 1 + bevel;
        float b = bevel / 2;

        void PartRotateOnY()
        {
            // First triangle
            builder!.AddTriangle(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, h), mtx) }
            );

            // Same triangle but flipped
            builder!.AddTriangle(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, -h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -b), mtx) }
            );

            // Plane between both triangles
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, b), mtx) }
            );

            // Plane in the corner to next main triangle
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(1, 1 - bevel, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(bevel, 0, h), mtx) }
            );

            // Same as before but for the flipped triangle
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, -h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(1, 1 - bevel, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(bevel, 0, -h), mtx) }
            );

            // Connect the two planes from before
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(1, 1 - bevel, -b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(w, 1, b), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(1, 1 - bevel, b), mtx) }
            );
        }

        void PartUpDown()
        {
            // Upper part
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(bevel, 0, h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-bevel, 0, h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, -bevel, h), mtx) }
            );

            // Lower part
            builder!.AddPlane(
                new Vertex { Position = Vector3.Transform(new Vector3(0, bevel, -h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(bevel, 0, -h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(-bevel, 0, -h), mtx) },
                new Vertex { Position = Vector3.Transform(new Vector3(0, -bevel, -h), mtx) }
            );
        }

        #region Construction

        Reset();
        RotateOnX();

        for (int i = 0; i < 4; i++)
        {
            PartRotateOnY();
            RotateOnY();
        }

        Reset();
        RotateOnX();
        PartUpDown();

        #endregion


        return builder.GetModel(gl);
    }

    public static void Render(
        GL gl,
        RailSceneObj railSceneObj,
        CommonSceneParameters scene,
        RailGeometryParameters railGeometry,
        CommonMaterialParameters railMaterial,
        CommonMaterialParameters railPointMaterial
    )
    {
        scene.Transform = s_railTranslate;

        if (!RailMaterial.TryUse(gl, scene, railGeometry, railMaterial, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            if (RailMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
                gl.Uniform1(location, railSceneObj.PickingId);

            railSceneObj.RailModel.Draw(gl);
        }

        RailObj rail = railSceneObj.RailObj;

        switch (rail.PointType)
        {
            case RailPointType.Bezier:

                for (int i = 0; i < rail.Points.Count; i++)
                {
                    var pickingId = railSceneObj.PointsPickingIds[i];
                    var selected = railSceneObj.PointsSelected[i];
                    var transforms = railSceneObj.PointTransforms[i];

                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P0, selected.P0, false, transforms.P0);
                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P1, selected.P1, true, transforms.P1);
                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P2, selected.P2, true, transforms.P2);
                }

                break;

            case RailPointType.Linear:

                for (int i = 0; i < rail.Points.Count; i++)
                {
                    var pickingId = railSceneObj.PointsPickingIds[i];
                    var selected = railSceneObj.PointsSelected[i];
                    var transforms = railSceneObj.PointTransforms[i];

                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P0, selected.P0, false, transforms.P0);
                }

                break;
        }
    }

    public static void CleanUp(GL gl) => s_pointModel?.CleanUp(gl);

    private static void DrawRailPoint(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        uint pickingId,
        bool selected,
        bool isHandle,
        Matrix4x4 transform
    )
    {
        RenderableModel model = s_pointModel!;

        if (isHandle)
            model = s_pointHandleModel!;

        scene.Transform = transform;
        material.Selected = selected;

        if (!RailPointMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            if (RailPointMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
                gl.Uniform1(location, pickingId);

            model.Draw(gl);
        }
    }
}
