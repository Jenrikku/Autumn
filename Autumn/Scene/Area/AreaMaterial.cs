using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Scene.Area;

internal static class AreaMaterial
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

            out vec3 vPos;

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos, 1.0);

                vPos = aPos;
            }
            """
        );

    private static readonly ShaderSource s_fragmentShader =
        new(
            "Area.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            in vec3 vPos;

            layout(std140) uniform ubMaterial {
                vec4 uColor;
                vec4 uHighlightColor;
            };

            out vec4 oColor;

            void main() {
                vec3 absolute = abs(vPos);

                float a = max3(
                    min(absolute.x, absolute.y),
                    min(absolute.x, absolute.z),
                    min(absolute.y, absolute.z));

                float wa = fwidth(a);

                float outline = smoothstep(0.5 - wa * 2, 0.5 - wa, a);

                oColor = mix(vec4(0, 0, 0, 0), uColor, outline);

                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
            }
            """
        );

    private static readonly ShaderProgram s_program = new(s_vertexShader, s_fragmentShader);

    public static bool TryUse(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) =>
        s_program.TryUse(
            gl,
            null,
            new ShaderParams[] { scene.ShaderParameters, material.ShaderParameters },
            out scope,
            out _
        );
}
