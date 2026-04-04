using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Area;

internal static class AreaMaterial
{
    private static readonly ShaderSource s_vertexCenterShader =
        new(
            "AreaCentered.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec2 aUV;
            layout(location = 2) in float aFaceType;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            out vec3 vPos;
            out vec2 vUV;
            out float vFaceType;
            out mat4x4 vTrans;

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos.x, aPos.y, aPos.z, 2.0);
                vPos = aPos;
                vTrans = uTransform;
                vUV = aUV;
                vFaceType = aFaceType;
            }
            """
        );
    private static readonly ShaderSource s_vertexBaseShader =
        new(
            "AreaBase.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec2 aUV;
            layout(location = 2) in float aFaceType;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            out vec3 vPos;
            out vec2 vUV;    
            out float vFaceType;
            out mat4x4 vTrans;

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos.x, aPos.y + 10.0, aPos.z, 2.0);
                vPos = aPos;
                vTrans = uTransform;
                vUV = aUV;
                vFaceType = aFaceType;
            }
            """
        );

    private static readonly ShaderSource s_CubeFragment =
        new(
            "CubeArea.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            in vec3 vPos;
            in vec2 vUV;
            in float vFaceType;
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
                                    length(vec3(vTrans[0][2], vTrans[1][2], vTrans[2][2])));
                oColor.a = 1.0;
                oPickingId = uPickingId;
                oPostProc = vec4((uHighlightColor.a > 0.1 ? 1 : 0), 0, 0, 0);
                oColor.rgb = vec3(vUV,0);//uColor.rgb;
                float xMin = 0.05;
                float yMin = 0.05;
                float dist = length(vUV - 0.5);
                vec3 col = vec3(0);
                float sz = 0.45;
                if (vFaceType < 0.8)
                {
                    oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                    oColor.rgb += (vUV.y < yMin || vUV.y > 1.0 - yMin) ? vec3(1) : vec3(0);
                    if (oColor.r < 0.1) discard;
                    vec2 nUV = vUV.x < 0.5 ? vUV : 1.0 - vUV;
                    col = vec3(nUV.xxx);
                    nUV = vUV.y < 0.5 ? vUV : 1.0 - vUV;
                    col += vec3(nUV.yyy);
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1)); //= col;
                    //oColor.rgb *= col;
                    //oColor.rgb = vec3(nUV.yy, 0);
                }
                if (vFaceType < 1.8)
                {
                    xMin = 0.05 / scale.x;
                    yMin = 0.05 / scale.z;
                    oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                    oColor.rgb += (vUV.y < yMin || vUV.y > 1.0 - yMin) ? vec3(1) : vec3(0);
                    if (oColor.r < 0.1) discard;
                    vec2 nUV = vUV.x < 0.5 ? vUV : 1.0 - vUV;
                    col = vec3(nUV.xxx);
                    nUV = vUV.y < 0.5 ? vUV : 1.0 - vUV;
                    col += vec3(nUV.yyy);
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1)); //= col;
                    //oColor.rgb *= col;
                    //oColor.rgb = vec3(nUV.yy, 0);
                }
                else if (vFaceType < 2.8)
                {
                    xMin = 0.05 / scale.x;
                    yMin = 0.05 / scale.z;
                    oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                    oColor.rgb += (vUV.y < xMin || vUV.y > 1.0 - xMin) ? vec3(1) : vec3(0);
                    
                    float v = (scale.z + scale.x) / 2;
                    vec2 rUV = rotate((vUV - 0.5) * v, 45) + 0.5;
                    col = vec3(rUV.x > sz && rUV.x < 1-sz ? 1 : 0);
                    col += vec3(rUV.y > sz && rUV.y < 1-sz ? 1 : 0);
                    oColor.rgb += col;
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                    
                    if ((oColor.r < 0.1)) discard;
                    
                }
                oColor.rgb = mix(uColor.rgb, uColor.rgb, oColor.rgb);
                //oColor.rgb *= uColor.rgb * 2;
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
                oColor.rgb = gl_FrontFacing ? oColor.rgb : oColor.rgb *0.80;
                // oColor.rgb = scale;
            }
            """
        );

    private static readonly ShaderSource s_SphereFragment =
        new(
            "SphereArea.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            in vec3 vPos;
            in vec2 vUV;
            in float vFaceType;
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
                oColor.a = 1.0;
                oPickingId = uPickingId;
                oPostProc = vec4((uHighlightColor.a > 0.1 ? 1 : 0), 0, 0, 0);
                oColor.rgb = vec3(vUV,0);//uColor.rgb;
                float xMin = 0.1;
                float yMin = 0.1;
                vec3 col = vec3(0);
                oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                oColor.rgb += (vUV.y < yMin || vUV.y > 1.0 - yMin) ? vec3(1) : vec3(0);
                if (oColor.r < 0.1) discard;
                vec2 nUV = vUV.x < 0.5 ? vUV : 1.0 - vUV;
                col = vec3(nUV.xxx);
                nUV = vUV.y < 0.5 ? vUV : 1.0 - vUV;
                col += vec3(nUV.yyy);

                oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                oColor.rgb = mix(uColor.rgb, uColor.rgb, oColor.rgb);
                
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
                oColor.rgb = gl_FrontFacing ? oColor.rgb : oColor.rgb *0.80;

            }
            """
        );
    private static readonly ShaderSource s_CylinderFragment =
        new(
            "CylinderArea.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            float max3(float a, float b, float c) {
                return max(max(a, b), c);
            }

            in vec3 vPos;
            in vec2 vUV;
            in float vFaceType;
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
                                    length(vec3(vTrans[0][2], vTrans[1][2], vTrans[2][2])));
                oColor.a = 1.0;
                oPickingId = uPickingId;
                oPostProc = vec4((uHighlightColor.a > 0.1 ? 1 : 0), 0, 0, 0);
                oColor.rgb = vec3(vUV,0);//uColor.rgb;
                float xMin = 0.05;
                float yMin = 0.1;
                float dist = length(vUV - 0.5);
                vec3 col = vec3(0);
                float sz = 0.45;
                if (vFaceType <0.8) // Sides
                {
                    oColor.rgb = (vUV.x < xMin || vUV.x > 1.0 - xMin) ? vec3(1) : vec3(0);
                    oColor.rgb += (vUV.y < yMin || vUV.y > 1.0 - yMin) ? vec3(1) : vec3(0);
                    if (oColor.r < 0.1) discard;
                    vec2 nUV = vUV.x < 0.5 ? vUV : 1.0 - vUV;
                    col = vec3(nUV.xxx);
                    nUV = vUV.y < 0.5 ? vUV : 1.0 - vUV;
                    col += vec3(nUV.yyy);
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1)); //= col;
                    //oColor.rgb *= col;
                    //oColor.rgb = vec3(nUV.yy, 0);
                }
                else if (vFaceType < 1.8) // Top Face
                {
                    
                    oColor.rgb = vec3(dist > 0.43 && dist < 0.5 ? 1 : 0);
                    if (!(dist > 0.43 && dist < 0.5)) discard;
                    col = vec3(dist * 0.8);
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                    //oColor.rgb = col;
                }
                else if (vFaceType < 2.8) // Bottom Face
                {
                    oColor.rgb = vec3(dist > 0.43 && dist < 0.5 ? 1 : 0);
                    col = vec3(dist * 0.8);
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                    
                    float v = (scale.z + scale.x) / 2;
                    vec2 rUV = rotate((vUV - 0.5) * v, 45) + 0.5;
                    col = vec3(rUV.x > sz && rUV.x < 1-sz ? 1 : 0);
                    col += vec3(rUV.y > sz && rUV.y < 1-sz ? 1 : 0);
                    oColor.rgb += col;
                    oColor.rgb = clamp(oColor.rgb, vec3(0), vec3(1));
                    oColor.rgb = dist < 0.5 ? oColor.rgb : vec3(0);
                    if ((oColor.r < 0.1)) discard;
                }
                oColor.rgb = mix(uColor.rgb, uColor.rgb, oColor.rgb);
                //oColor.rgb *= uColor.rgb * 2;
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
                oColor.rgb = gl_FrontFacing ? oColor.rgb : oColor.rgb *0.80;
            }
            """
        );

    public static readonly ShaderProgram CenterCubeProgram = new(s_vertexCenterShader, s_CubeFragment);
    public static readonly ShaderProgram BaseCubeProgram = new(s_vertexBaseShader, s_CubeFragment);
    public static readonly ShaderProgram SphereProgram = new(s_vertexCenterShader, s_SphereFragment);
    public static readonly ShaderProgram CylinderProgram = new(s_vertexBaseShader, s_CylinderFragment);

    public static bool TryUse(
        GL gl,
        int AreaType,
        CommonSceneParameters scene,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    )
    {
        return AreaType switch
        {
            1 => CenterCubeProgram.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _),
            2 => SphereProgram.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _),
            3 => CylinderProgram.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _),
            _ => BaseCubeProgram.TryUse(gl, null, [scene.ShaderParameters, material.ShaderParameters], out scope, out _),

        }; 
    }

    public static Vector4 GetAreaColor(string name)
    {
        return name switch
        {
            "AudioEffectChangeArea" => new Vector4(0.0f, 0.4f, 1.0f, 1.0f),
            "AudioVolumeSettingArea" => new Vector4(0.22f, 1.0f, 0.08f, 1.0f),
            "BgmChangeArea" => new Vector4(1.0f, 0.08f, 0.58f, 1.0f),
            "CameraArea" => new Vector4(1.0f, 0.0f, 0.2f, 1.0f),
            "CameraOriginArea" => new Vector4(1.0f, 0.65f, 0.0f, 1.0f),
            "CameraWaveArea" => new Vector4(1.0f, 1.0f, 0.2f, 1.0f),
            "ChangeCoverArea" => new Vector4(0.0f, 0.8f, 1.0f, 1.0f),
            "DeathArea" => new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
            "EnablePropellerFallCameraArea" => new Vector4(1.0f, 0.4f, 0.0f, 1.0f),
            "FogArea" => new Vector4(0.8f, 0.0f, 0.8f, 1.0f),
            "FogAreaCameraPos" => new Vector4(0.0f, 0.6f, 0.6f, 1.0f),
            "FootPrintFollowPosArea" => new Vector4(1.0f, 0.2f, 0.4f, 1.0f),
            "InvalidatePropellerCameraArea" => new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
            "KinopioHouseExitArea" => new Vector4(1.0f, 0.4f, 0.0f, 1.0f),
            "LightArea" => new Vector4(0.8f, 1.0f, 0.0f, 1.0f),
            "ObjectChildArea" => new Vector4(0.13f, 0.13f, 0.42f, 1.0f),
            "PlayerAlongWallArea" => new Vector4(1.0f, 0.0f, 0.5f, 1.0f),
            "PlayerControlOffArea" => new Vector4(1.0f, 0.4f, 0.4f, 1.0f),
            "PlayerInclinedControlArea" => new Vector4(0.29f, 0.0f, 0.51f, 1.0f),
            "PlayerRestrictedPlane" => new Vector4(0.8f, 1.0f, 0.0f, 1.0f),
            "PlayerWidenStickXSnapArea" => new Vector4(0.6f, 0.4f, 0.8f, 1.0f),
            "PresentMessageArea" => new Vector4(1.0f, 1.0f, 0.4f, 1.0f),
            "SoundEmitArea" => new Vector4(0.0f, 1.0f, 0.8f, 1.0f),
            "SpotLightArea" => new Vector4(0.75f, 0.75f, 0.75f, 1.0f),
            "StickFixArea" => new Vector4(0.0f, 1.0f, 0.4f, 1.0f),
            "StickSnapOffArea" => new Vector4(1.0f, 0.6f, 0.0f, 1.0f),
            "SwitchKeepOnArea" => new Vector4(1.0f, 0.2f, 0.6f, 1.0f),
            "SwitchOnArea" => new Vector4(0.0f, 0.8f, 0.8f, 1.0f),
            "ViewCtrlArea" => new Vector4(1.0f, 0.6f, 0.0f, 1.0f),
            "WaterArea" => new Vector4(0.8f, 0.4f, 1.0f, 1.0f),
            "WaterFallArea" => new Vector4(1.0f, 0.08f, 0.58f, 1.0f),
            "WaterFlowArea" => new Vector4(1.0f, 1.0f, 0.2f, 1.0f),
            "GhostPlayerArea" => new Vector4(0.6f, 0.2f, 1.0f, 1.0f),
            "Guide3DArea" => new Vector4(0.0f, 0.6f, 0.8f, 1.0f),
            "MessageArea" => new Vector4(0.0f, 0.6f, 0.6f, 1.0f),
            "BugFixBalanceTruckArea" => new Vector4(1.0f, 0.4f, 0.0f, 1.0f),
            _ => new Vector4(1.0f)
        };
    }
}
