using Autumn.Scene.DefaultCube;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Scene.Area;

internal static class AreaRenderer
{
    private static RenderableModel? s_model;

    public static void Initialize(GL gl) => s_model = DefaultCubeRenderer.GenerateCubeModel(gl, 10);

    public static void Render(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        uint pickingId
    )
    {
        if (!AreaMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.Disable(EnableCap.CullFace);

            if (AreaMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
                gl.Uniform1(location, pickingId);

            s_model!.Draw(gl);

            gl.Enable(EnableCap.CullFace);
        }
    }

    public static void CleanUp(GL gl)
    {
        s_model?.CleanUp(gl);
    }
}
