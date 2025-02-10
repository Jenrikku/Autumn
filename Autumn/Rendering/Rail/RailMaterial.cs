using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailMaterial
{
    private static readonly ShaderSource s_vertexShader =
        new(
            "Area.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            void main() {
                gl_Position = uViewProjection * uTransform;
            }
            """
        );

    private static readonly ShaderSource s_fragmentShader =
        new(
            "Area.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            layout(std140) uniform ubMaterial {
                vec4 uColor;
                vec4 uHighlightColor;
            };

            uniform uint uPickingId;

            out vec4 oColor;
            out uint oPickingId;

            void main() {
                oPickingId = uPickingId;

                oColor = uColor;
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
            }
            """
        );

    public static readonly ShaderProgram Program = new(s_vertexShader, s_fragmentShader);

    public static bool TryUse(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) => Program.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _);
}
