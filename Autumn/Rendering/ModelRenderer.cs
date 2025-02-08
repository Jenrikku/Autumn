using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Area;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.DefaultCube;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class ModelRenderer
{
    private static readonly Vector3 s_highlightColor = new(1, 1, 0);

    private static CommonSceneParameters? s_commonSceneParams;
    private static CommonMaterialParameters? s_defaultCubeMaterialParams;
    private static CommonMaterialParameters? s_areaMaterialParams;

    private static Matrix4x4 s_viewMatrix = Matrix4x4.Identity;
    private static Matrix4x4 s_projectionMatrix = Matrix4x4.Identity;

    private static Matrix4x4 s_h3DScale = Matrix4x4.CreateScale(0.01f);

    public static bool VisibleAreas = false;
    public static bool VisibleCameraAreas = true;

    public static void Initialize(GL gl)
    {
        DefaultCubeRenderer.Initialize(gl);
        AreaRenderer.Initialize(gl);

        s_commonSceneParams = new(gl);
        s_defaultCubeMaterialParams = new(gl, new(1, 0.5f, 0, 1), s_highlightColor);
        s_areaMaterialParams = new(gl, new(0, 1, 0, 1), s_highlightColor);
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
        if (s_commonSceneParams is null || s_defaultCubeMaterialParams is null || s_areaMaterialParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(Draw)}"
            );

        StageObj stageObj = sceneObj.StageObj;
        Actor actor = sceneObj.Actor;

        if (!sceneObj.IsVisible)
            return;

        if (
            stageObj.Type == StageObjType.Area
            || stageObj.Type == StageObjType.CameraArea
            || stageObj.Type == StageObjType.AreaChild
        )
        {
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
                _ => new Vector4(1.0f)
            };

            if (
                !VisibleAreas
                && !sceneObj.Selected
                && (sceneObj.StageObj.Type == StageObjType.Area || sceneObj.StageObj.Type == StageObjType.AreaChild)
            )
                return;

            if (!VisibleCameraAreas && !sceneObj.Selected && sceneObj.StageObj.Type == StageObjType.CameraArea)
                return;

            sceneObj.Actor.AABB = new AxisAlignedBoundingBox(20f);

            gl.CullFace(TriangleFace.Back);

            AreaRenderer.Render(gl, s_commonSceneParams, s_areaMaterialParams, sceneObj.PickingId);
            return;
        }

        if (stageObj is RailObj rail)
        {
            return;
        }

        if (actor.IsEmptyModel)
        {
            s_commonSceneParams.Transform = sceneObj.Transform;
            s_defaultCubeMaterialParams.Selected = sceneObj.Selected;
            sceneObj.Actor.AABB = new AxisAlignedBoundingBox(2f);

            gl.CullFace(TriangleFace.Back);

            DefaultCubeRenderer.Render(gl, s_commonSceneParams, s_defaultCubeMaterialParams, sceneObj.PickingId);
        }

        foreach (H3DMeshLayer layer in Enum.GetValues<H3DMeshLayer>())
        foreach (var (mesh, material) in actor.EnumerateMeshes(layer))
        {
            material.SetSelectionColor(new(s_highlightColor, sceneObj.Selected ? 0.4f : 0));

            material.SetMatrices(s_projectionMatrix, s_h3DScale * sceneObj.Transform, s_viewMatrix);

            material.TryUse(gl, out ProgramUniformScope scope);

            using (scope)
            {
                if (material.CullFaceMode == TriangleFace.FrontAndBack)
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

                    gl.BlendEquationSeparate(material.ColorBlendEquation, material.AlphaBlendEquation);

                    gl.BlendFuncSeparate(
                        material.ColorSrcFact,
                        material.ColorDstFact,
                        material.AlphaSrcFact,
                        material.AlphaDstFact
                    );
                }

                gl.StencilFunc(material.StencilFunction, material.StencilRef, material.StencilMask);

                gl.StencilMask(material.StencilBufferMask);

                gl.StencilOp(material.StencilOps[0], material.StencilOps[1], material.StencilOps[2]);

                gl.DepthFunc(material.DepthFunction);
                gl.DepthMask(material.DepthMaskEnabled);

                gl.ColorMask(
                    material.ColorMask[0],
                    material.ColorMask[1],
                    material.ColorMask[2],
                    material.ColorMask[3]
                );

                if (material.PolygonOffsetFillEnabled)
                {
                    gl.Enable(EnableCap.PolygonOffsetFill);
                    gl.PolygonOffset(0, material.PolygonOffsetUnit);
                }

                material.Program.TryGetUniformLoc("uPickingId", out int location);
                gl.Uniform1(location, sceneObj.PickingId);

                mesh.Draw();

                gl.Enable(EnableCap.CullFace);
                gl.Disable(EnableCap.PolygonOffsetFill);
                gl.Disable(EnableCap.Blend);
            }
        }
    }
}
