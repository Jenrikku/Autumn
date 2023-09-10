using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Autumn.Scene.DefaultCube;

internal static class DefaultCubeMaterial
{
    private static readonly ShaderSource s_vertexShader =
        new(
            "DefaultCube.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            out vec3 vPos;
            out vec3 boxScale;

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos, 1.0);

                boxScale = vec3(
                    length(uTransform[0].xyz),
                    length(uTransform[1].xyz),
                    length(uTransform[2].xyz));

                vPos = aPos;
            }
            """
        );

    private static readonly ShaderSource s_fragmentShader =
        new(
            "DefaultCube.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            float eval(float signedDistance) {
                return smoothstep(0.0, fwidth(signedDistance), -signedDistance);
            }

            // https://iquilezles.org/articles/distfunctions/
            float sdCapsule(vec2 p, vec2 a, vec2 b, float r) {
                vec2 pa = p - a, ba = b - a;
                float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
                return length(pa - ba * h) - r;
            }

            in vec3 vPos;
            in vec3 boxScale;

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
                    min(absolute.x, absolute.z),
                    min(absolute.y, absolute.z));

                float wa = fwidth(a);

                float outline = smoothstep(0.5 - wa * 2, 0.5 - wa, a);
                    
                float s = min(boxScale.x, boxScale.z);
                float r = 0.05 * min(boxScale.x, boxScale.z);
                vec2 pos2d = (vPos * boxScale).xz;

                float d = min(
                    sdCapsule(pos2d, vec2(-0.25, 0) * s, vec2(0, 0.25) * s, r),
                    sdCapsule(pos2d, vec2(0.25, 0) * s, vec2(0, 0.25) * s, r));

                oColor = mix(uColor * 0.18, uColor, outline);
                oColor = mix(oColor, uColor * 0.5, eval(d));

                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
            }
            """
        );

    public static ShaderProgram Program { get; } = new(s_vertexShader, s_fragmentShader);

    public static bool TryUse(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) =>
        Program.TryUse(
            gl,
            null,
            new ShaderParams[] { scene.ShaderParameters, material.ShaderParameters },
            out scope,
            out _
        );
}
