using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailPointMaterial
{
    private static readonly ShaderSource s_vertexShader =
        new(
            "RailPoint.vert",
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
            "RailPoint.frag",
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

            uniform uint uPickingId;

            out vec4 oColor;
            out uint oPickingId;

            void main() {
                oPickingId = uPickingId;
                
                vec3 absolute = abs(vPos);

                float a = max3(
                    min(absolute.x, absolute.y),
                    min(absolute.y, absolute.z),
                    min(absolute.x, -absolute.z));

                float wa = fwidth(a);

                float outline = smoothstep(0.01 - wa, 0.17 - wa, a);
                
                //vec4 glint = vec4(1,0.88,0.2,1) + 0.06; // YELLOW
                //vec4 col = vec4(0.93,0.8,0,1); 
                //vec4 glint = vec4(1,0.4,0.4,1) + 0.06; // RED
                //vec4 col = vec4(1,0,0.24,1);
                vec4 glint = vec4(0.38,0.69,1,1) + 0.06; // BLUE
                vec4 col = vec4(0.09,0.5,0.86,1);
                oColor = mix(glint, col, outline);
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
                oColor.a = 1;
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
