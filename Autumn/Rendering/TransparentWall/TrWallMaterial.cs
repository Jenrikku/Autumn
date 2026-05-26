using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class TransparentWallMaterial
{
    private static readonly ShaderSource s_WallVertex =
        new(
            "TransparentWall.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec2 aUV;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            out vec3 vPos;
            out vec2 vUV;
            out mat4x4 vTrans;

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos.x, aPos.y, aPos.z, 2.0);
                vPos = aPos;
                vTrans = uTransform;
                vUV = aUV;
            }
            """
        );

    private static readonly ShaderSource s_WallFragment =
        new(
            "TransparentWall.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            in vec3 vPos;
            in vec2 vUV;
            in mat4x4 vTrans;

            layout(std140) uniform ubMaterial {
                vec4 uColor;
                vec4 uHighlightColor;
            };

            uniform uint uPickingId;

            out vec4 oColor;
            out uint oPickingId;
            out vec4 oPostProc;

            const float PI = 3.14159;

            vec2 rotate(vec2 samplePosition, float rotation){
                float angle = rotation * PI / 180;
                return vec2(cos(angle) * samplePosition.x + sin(angle) * samplePosition.y, 
                            cos(angle) * samplePosition.y - sin(angle) * samplePosition.x);
            }

            void main() {
                vec3 scale = vec3(  length(vec3(vTrans[0][0], vTrans[1][0], vTrans[2][0])),
                                    length(vec3(vTrans[0][1], vTrans[1][1], vTrans[2][1])), 
                                    length(vec3(vTrans[0][2], vTrans[1][2], vTrans[2][2]))) * 100;
                oColor.a = 1.0;
                oPickingId = uPickingId;
                oPostProc = vec4((uHighlightColor.a > 0.1 ? 1 : 0), 0, 0, 0);
                oColor.rgb = vec3(vUV,0);//uColor.rgb;
                float xMin = 0.3 / scale.x;
                float yMin = 0.3 / scale.y;
                float dist = length(vUV - 0.5);
                vec3 col = vec3(0);
                float sz = 0.45;

                oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                oColor.rgb += (vUV.y < yMin || vUV.y > 1.0 - yMin) ? vec3(1) : vec3(0);
                
                float v = (scale.y + scale.x) / 2;
                vec2 rUV = rotate((vUV - 0.5) * v, 45) + 0.5;
                col = vec3(rUV.x > sz && rUV.x < 1-sz ? 1 : 0);
                col += vec3(rUV.y > sz && rUV.y < 1-sz ? 1 : 0);
                oColor.rgb += col;
                oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                
                if ((oColor.r < 0.1)) discard;

                oColor.rgb = mix(uColor.rgb, uColor.rgb, oColor.rgb);
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
                oColor.rgb = gl_FrontFacing ? oColor.rgb : oColor.rgb *0.80;
            }
            """
        );

    public static readonly ShaderProgram WallProgram = new(s_WallVertex, s_WallFragment);
    public static bool TryUse(
        GL gl,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) => WallProgram.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _);
    
}
