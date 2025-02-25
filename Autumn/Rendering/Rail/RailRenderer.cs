using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailRenderer
{
    private static readonly Matrix4x4 s_pointHandleScale = Matrix4x4.CreateScale(0.5f);

    private static RenderableModel? s_pointModel;

    public static void Initialize(GL gl) => s_pointModel = DefaultCubeRenderer.GenerateCubeModel(gl, 0.5f);

    public static void Render(
        GL gl,
        RailSceneObj railSceneObj,
        CommonSceneParameters scene,
        CommonMaterialParameters railMaterial,
        CommonMaterialParameters railPointMaterial
    )
    {
        if (!RailMaterial.TryUse(gl, scene, railMaterial, out ProgramUniformScope scope))
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
                    RailPointBezier point = (RailPointBezier)rail.Points[i];

                    var pickingId = railSceneObj.PointsPickingIds[i];
                    var selected = railSceneObj.PointsSelected[i];
                    var transforms = railSceneObj.PointTransforms[i];

                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P0, selected.P0, true, transforms.P0);
                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P1, selected.P1, false, transforms.P1);
                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P2, selected.P2, true, transforms.P2);
                }

                break;

            case RailPointType.Linear:

                for (int i = 0; i < rail.Points.Count; i++)
                {
                    RailPointLinear point = (RailPointLinear)rail.Points[i];

                    var pickingId = railSceneObj.PointsPickingIds[i];
                    var selected = railSceneObj.PointsSelected[i];
                    var transforms = railSceneObj.PointTransforms[i];

                    DrawRailPoint(gl, scene, railPointMaterial, pickingId.P1, selected.P1, false, transforms.P1);
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
        if (isHandle) transform = s_pointHandleScale * transform;

        if (RailMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
            gl.Uniform1(location, pickingId);

        scene.Transform = transform;
        material.Selected = selected;

        if (!RailMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            s_pointModel!.Draw(gl);
        }
    }
}
