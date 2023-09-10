using Autumn.Scene.DefaultCube;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Scene.Area;

internal static class AreaRenderer
{
    private static RenderableModel? s_model;

    public static void Initialize(GL gl) => s_model = DefaultCubeRenderer.GenerateCubeModel(gl, 10);

    public static void Render(GL gl, CommonSceneParameters scene, CommonMaterialParameters material)
    {
        if (!AreaMaterial.TryUse(gl, scene, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.Disable(EnableCap.CullFace);

            s_model!.Draw(gl);

            gl.Enable(EnableCap.CullFace);
        }
    }

    public static void CleanUp(GL gl)
    {
        s_model?.CleanUp(gl);
    }
}
