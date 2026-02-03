using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Area;

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
                gl_Position = uViewProjection * uTransform * vec4(aPos.x, aPos.y + 10.0, aPos.z, 2.0);
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

            uniform uint uPickingId;

            out vec4 oColor;
            out uint oPickingId;

            void main() {                
                vec3 absolute = abs(vPos);

            float a = max3(
                min(absolute.x, absolute.y),
                min(absolute.x, absolute.z),
                min(absolute.y, absolute.z));

            float wa = fwidth(a);

            float outline = smoothstep(9 - wa , 10 + wa, a);
            float outline2 = smoothstep(9 - wa , 10 + wa, a * 0.95);

            if (outline < 0.6) discard;
            oColor.rgb = mix(uColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
            oColor.rgb = gl_FrontFacing ? oColor.rgb : oColor.rgb *0.85;
            oColor.rgb += vec3(clamp(outline2 * 0.5- 0.2,0,1));
            
            oColor.a = 1.0;
            oPickingId = uPickingId;

            // oColor = mix(vec4(0, 0, 0, 0), uColor, outline);

            // oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
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
