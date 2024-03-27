using System.Numerics;
using Autumn.Scene.Area;
using Autumn.Scene.DefaultCube;
using Autumn.Scene.H3D;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Scene;

internal static class ModelRenderer
{
    private static CommonSceneParameters? s_commonSceneParams;
    private static CommonMaterialParameters? s_defaultCubeMaterialParams;
    private static CommonMaterialParameters? s_areaMaterialParams;

    private static Matrix4x4 s_viewMatrix = Matrix4x4.Identity;
    private static Matrix4x4 s_projectionMatrix = Matrix4x4.Identity;

    private static Matrix4x4 s_h3DScale = Matrix4x4.CreateScale(0.01f);

    public static void Initialize(GL gl)
    {
        DefaultCubeRenderer.Initialize(gl);
        AreaRenderer.Initialize(gl);

        s_commonSceneParams = new(gl);
        s_defaultCubeMaterialParams = new(gl, new(1, 0.5f, 0, 1), new(1, 1, 0));
        s_areaMaterialParams = new(gl, new(0, 1, 0, 1), new(1, 1, 0));
    }

    public static void UpdateMatrices(in Matrix4x4 view, in Matrix4x4 projection)
    {
        if (s_commonSceneParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(UpdateMatrices)}"
            );

        s_viewMatrix = view;
        s_projectionMatrix = projection;

        s_commonSceneParams.ViewProjection = view * projection;
    }

    public static void Draw(GL gl, SceneObj sceneObj)
    {
        if (
            s_commonSceneParams is null
            || s_defaultCubeMaterialParams is null
            || s_areaMaterialParams is null
        )
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(Draw)}"
            );

        StageObj stageObj = sceneObj.StageObj;
        ActorObj actorObj = sceneObj.ActorObj;

        if (
            stageObj.Type == StageObjType.Area
            || stageObj.Type == StageObjType.CameraArea
            || stageObj.Type == StageObjType.AreaChild
        )
        {
            // TO-DO: Change color based on the name here.

            s_commonSceneParams.Transform = sceneObj.Transform;
            s_areaMaterialParams.Selected = sceneObj.Selected;

            s_areaMaterialParams.Color = stageObj.Name switch
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
                _ => throw new ArgumentException("Invalid area name")
            };

            gl.CullFace(TriangleFace.Back);

            AreaRenderer.Render(gl, s_commonSceneParams, s_areaMaterialParams, sceneObj.PickingId);
            return;
        }

        if (actorObj.IsNoModel)
        {
            s_commonSceneParams.Transform = sceneObj.Transform;
            s_defaultCubeMaterialParams.Selected = sceneObj.Selected;

            gl.CullFace(TriangleFace.Back);

            DefaultCubeRenderer.Render(
                gl,
                s_commonSceneParams,
                s_defaultCubeMaterialParams,
                sceneObj.PickingId
            );
        }
        else
        {
            if (actorObj.RenderableModels.Length <= 0)
                H3DRenderingGenerator.GenerateMaterialsAndModels(gl, actorObj);

            for (int i = 0; i < 3; i++)
            for (int j = 0; j < actorObj.RenderingMaterials.Length; j++)
            {
                RenderableModel model = actorObj.RenderableModels[j];
                H3DRenderingMaterial material = actorObj.RenderingMaterials[j];

                if ((int)material.Layer != i)
                    continue;

                material.SetSelectionColor(new(1, 1, 0, sceneObj.Selected ? 0.4f : 0));

                material.SetMatrices(
                    s_projectionMatrix,
                    s_h3DScale * sceneObj.Transform,
                    s_viewMatrix
                );

                material.TryUse(gl, out ProgramUniformScope scope);

                using (scope)
                {
                    if (material.CullFaceMode == 0)
                        gl.Disable(EnableCap.CullFace);
                    else
                        gl.CullFace(material.CullFaceMode);

                    if (material.BlendingEnabled)
                    {
                        gl.Enable(EnableCap.Blend);

                        gl.BlendColor(
                            material.BlendingColor.X,
                            material.BlendingColor.Y,
                            material.BlendingColor.Z,
                            material.BlendingColor.W
                        );

                        gl.BlendEquationSeparate(
                            material.ColorBlendEquation,
                            material.AlphaBlendEquation
                        );

                        gl.BlendFuncSeparate(
                            material.ColorSrcFact,
                            material.ColorDstFact,
                            material.AlphaSrcFact,
                            material.AlphaDstFact
                        );
                    }

                    material.Program.TryGetUniformLoc("uPickingId", out int location);
                    gl.Uniform1(location, sceneObj.PickingId);

                    model.Draw(gl);

                    gl.Enable(EnableCap.CullFace);
                    gl.Disable(EnableCap.Blend);
                }
            }
        }
    }
}
