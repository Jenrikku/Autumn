using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Area;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class ModelRenderer
{
    private static readonly Vector3 s_highlightColor = new(1, 1, 0);

    private static CommonSceneParameters? s_commonSceneParams;
    private static CommonMaterialParameters? s_defaultCubeMaterialParams;

    private static Matrix4x4 s_viewMatrix = Matrix4x4.Identity;
    private static Matrix4x4 s_projectionMatrix = Matrix4x4.Identity;

    public static bool VisibleAreas = false;
    public static bool VisibleCameraAreas = true;

    public static void Initialize(GL gl)
    {
        DefaultCubeRenderer.Initialize(gl);
        AreaRenderer.Initialize(gl);

        s_commonSceneParams = new();
        s_defaultCubeMaterialParams = new(new(1, 0.5f, 0, 1), s_highlightColor);
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

    public static void Draw(GL gl, ISceneObj sceneObj)
    {
        if (s_commonSceneParams is null || s_defaultCubeMaterialParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(Draw)}"
            );

        StageObj stageObj = sceneObj.StageObj;

        if (!sceneObj.IsVisible)
            return;

        if (sceneObj is BasicSceneObj basicSceneObj && stageObj.IsArea())
        {
            if (!VisibleAreas && !sceneObj.Selected && stageObj.Type != StageObjType.CameraArea)
                return;

            if (!VisibleCameraAreas && !sceneObj.Selected && sceneObj.StageObj.Type == StageObjType.CameraArea)
                return;

            s_commonSceneParams.Transform = sceneObj.Transform;

            if (basicSceneObj.Selected)
            {
                basicSceneObj.MaterialParams.HighlightColor = s_highlightColor;
                basicSceneObj.MaterialParams.Selected = true;
            }
            else if (basicSceneObj.MaterialParams.Selected)
            {
                // We only set it to false if needed, otherwise the buffer will be rewritten always.
                basicSceneObj.MaterialParams.Selected = false;
            }

            gl.CullFace(TriangleFace.Back);

            AreaRenderer.Render(gl, s_commonSceneParams, basicSceneObj.MaterialParams, sceneObj.PickingId);
            return;
        }

        if (sceneObj is RailSceneObj railSceneObj)
        {
            return;
        }

        if (sceneObj is ActorSceneObj actorSceneObj)
        {
            Actor actor = actorSceneObj.Actor;

            if (actor.IsEmptyModel)
            {
                s_commonSceneParams.Transform = sceneObj.Transform;
                s_defaultCubeMaterialParams.Selected = sceneObj.Selected;
                actorSceneObj.AABB = new AxisAlignedBoundingBox(2f);

                gl.CullFace(TriangleFace.Back);

                DefaultCubeRenderer.Render(gl, s_commonSceneParams, s_defaultCubeMaterialParams, sceneObj.PickingId);
                return;
            }

            foreach (H3DMeshLayer layer in Enum.GetValues<H3DMeshLayer>())
            foreach (var (mesh, material) in actor.EnumerateMeshes(layer))
            {
                material.SetSelectionColor(new(s_highlightColor, actorSceneObj.Selected ? 0.4f : 0));

                material.SetMatrices(s_projectionMatrix, actorSceneObj.Transform, s_viewMatrix);

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
                    gl.Uniform1(location, actorSceneObj.PickingId);

                    mesh.Draw();

                    gl.Enable(EnableCap.CullFace);
                    gl.Disable(EnableCap.PolygonOffsetFill);
                    gl.Disable(EnableCap.Blend);
                }
            }
        }
    }
}
