using Autumn.Enums;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailRenderer
{
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

                foreach (var point in rail.Points.Cast<RailPointBezier>())
                {
                    // TO-DO: Draw bezier point.
                }

                break;

            case RailPointType.Linear:

                foreach (var point in rail.Points.Cast<RailPointLinear>())
                {
                    // TO-DO: Draw linear point.
                }

                break;
        }
    }

    public static void CleanUp(GL gl) => s_pointModel?.CleanUp(gl);
}
