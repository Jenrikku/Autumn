using System.Diagnostics;
using System.Numerics;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.Rendering.Area;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Rail;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using SceneGL;
using SceneGL.GLHelpers;
using SceneGL.Materials.Common;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;

namespace Autumn.Rendering;

internal static class ModelRenderer
{
    private static readonly Vector3 s_highlightColor = new(1, 1, 0);

    private static CommonSceneParameters? s_commonSceneParams;
    private static CommonMaterialParameters? s_defaultCubeMaterialParams;

    private static RailGeometryParameters? s_railGeometryParams;
    private static CommonMaterialParameters? s_railMaterialParams;
    private static CommonMaterialParameters? s_railPointMaterialParams;

    private static Matrix4x4 s_viewMatrix = Matrix4x4.Identity;
    private static Matrix4x4 s_projectionMatrix = Matrix4x4.Identity;
    private static Vector3 s_cameraRotation;
    private static H3DRenderingMaterial.Light _defaultLight = new()
    {
        Ambient = new(0.1f, 0.1f, 0.1f, 1),
        Diffuse = new(0.4f, 0.4f, 0.4f, 1),
        Specular0 = new(0.8f, 0.8f, 0.8f, 1),
        Specular1 = new(0.4f, 0.4f, 0.4f, 1),
        Position = new(1, 1, 0.7f),
        Direction = new(0, 0, 0),
        Directional = 1,
        TwoSidedDiffuse = 0,
        DisableConst5 = 1
    };

    public static Dictionary<string, TextureSampler> GeneralLUTs = new();

    public static bool VisibleAreas = false;
    public static bool VisibleCameraAreas = true;
    public static bool VisibleRails = true;
    public static bool VisibleGrid = true;

    public static void Initialize(GL gl, LayeredFSHandler fsHandler)
    {
        DefaultCubeRenderer.Initialize(gl);
        AreaRenderer.Initialize(gl);

        s_commonSceneParams = new();

        s_defaultCubeMaterialParams = new(new(1, 0.5f, 0, 1), s_highlightColor);
        s_railGeometryParams = new(lineWidth: 0.15f, camera: new(1));
        s_railMaterialParams = new(new(0.75f, 0.5f, 0.5f, 1), s_highlightColor);
        s_railPointMaterialParams = new(new(1, 1, 0, 1), s_highlightColor);

        var narc = fsHandler.ReadShaders();
        if (narc is not null)
        {
            bool found = narc.TryGetFile("Shader.bcsdr", out byte[] cgfx);

            if (!found)
                return;

            H3D h3D;

            try
            {
                using MemoryStream stream = new(cgfx);
                h3D = Gfx.OpenAsH3D(stream);
            }
            catch
            {
                Debug.Write($"The cgfx could not be read", "Error");
                return;
            }
            foreach (H3DLUT lut in h3D.LUTs)
                foreach (H3DLUTSampler sampler in lut.Samplers)
                {
                    AddLUTTexture(gl, lut.Name, sampler);
                }
        }
    }

    public static void UpdateSceneParams(in Matrix4x4 view, in Matrix4x4 projection, in Quaternion cameraRot, in Vector3 cameraEye)
    {
        if (s_commonSceneParams is null || s_railGeometryParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(UpdateSceneParams)}"
            );

        s_viewMatrix = view;
        s_projectionMatrix = projection;
        s_cameraRotation = Vector3.Transform(Vector3.UnitZ, cameraRot);

        s_commonSceneParams.ViewProjection = view * projection;
        s_railGeometryParams.Camera = cameraEye;
    }

    public static void Draw(GL gl, ISceneObj sceneObj, StageLight? previewLight = null)
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
            if (!VisibleRails && !railSceneObj.Selected)
                return;
            s_railMaterialParams!.Selected = railSceneObj.Selected;

            gl.Disable(EnableCap.CullFace);
            RailRenderer.Render(gl, railSceneObj, s_commonSceneParams, s_railGeometryParams!, s_railMaterialParams, s_railPointMaterialParams!);
            return;
        }

        if (sceneObj is ActorSceneObj actorSceneObj)
        {
            Actor actor = actorSceneObj.Actor;

            if (actor.IsEmptyModel)
            {
                s_commonSceneParams.Transform = sceneObj.Transform;
                s_defaultCubeMaterialParams.Selected = sceneObj.Selected;

                gl.CullFace(TriangleFace.Back);

                DefaultCubeRenderer.Render(gl, s_commonSceneParams, s_defaultCubeMaterialParams, sceneObj.PickingId);
                return;
            }

            foreach (H3DMeshLayer layer in Enum.GetValues<H3DMeshLayer>())
            foreach (var (mesh, material) in actor.EnumerateMeshes(layer))
            {
                material.SetSelectionColor(new(s_highlightColor, actorSceneObj.Selected ? 0.4f : 0));
                material.SetMatrices(s_projectionMatrix, actorSceneObj.Transform, s_viewMatrix);
                material.SetLight0(previewLight?.GetAsLight() ?? _defaultLight);
                material.SetViewRotation(s_cameraRotation);

                if (!material.TryUse(gl, out ProgramUniformScope scope))
                    continue;

                using (scope)
                {
                    if (material.CullFaceMode == TriangleFace.FrontAndBack)
                        gl.Disable(EnableCap.CullFace);
                    else
                        gl.CullFace(material.CullFaceMode);

                    if (material.BlendingEnabled)
                    {
                        gl.Enable(EnableCap.Blend | (EnableCap)0x0B60);// Attempt at Fog rendering

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

    public static void AddLUTTexture(GL gl, string tableName, H3DLUTSampler sampler)
    {
        string name = tableName + sampler.Name;

        float[] table = new float[512];

        if ((sampler.Flags & H3DLUTFlags.IsAbsolute) != 0)
        {
            for (int i = 0; i < 256; i++)
            {
                table[i + 256] = sampler.Table[i];
                table[i + 0] = sampler.Table[0];
            }
        }
        else
        {
            for (int i = 0; i < 256; i += 2)
            {
                int PosIdx = i >> 1;
                int NegIdx = PosIdx + 128;

                table[i + 256] = sampler.Table[PosIdx];
                table[i + 257] = sampler.Table[PosIdx];
                table[i + 0] = sampler.Table[NegIdx];
                table[i + 1] = sampler.Table[NegIdx];
            }
        }

        uint glSampler = SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.NEAREST);

        uint glTexture = TextureHelper.CreateTexture2D<float>(
            gl,
            SceneGL.PixelFormat.R32_Float,
            (uint)table.Length,
            1,
            table,
            false
        );

        TextureSampler textureSampler = new(glSampler, glTexture);

        GeneralLUTs.Add(name, textureSampler);
    }
}
