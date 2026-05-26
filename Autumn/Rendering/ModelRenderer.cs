using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using Autumn.Context;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.Rendering.Area;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.DefaultCube;
using Autumn.Rendering.Rail;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using SceneGL;
using SceneGL.GLHelpers;
using SceneGL.Materials.Common;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model.Material;
using static Autumn.Wrappers.ClassModifiersWrapper;

namespace Autumn.Rendering;

internal static class ModelRenderer
{
    private static readonly Vector3 s_highlightColor = new(1, 1, 1);

    private static CommonSceneParameters? s_commonSceneParams;
    private static CommonMaterialParameters? s_defaultCubeMaterialParams;
    private static CommonMaterialParameters? s_transparentWallMaterialParams;

    private static RailGeometryParameters? s_railGeometryParams;
    private static RailGeometryParameters? s_railHandleGeoParams;
    private static RelationLineParams? s_relationParams;
    private static CommonMaterialParameters? s_relationMaterialParams;
    private static CommonMaterialParameters? s_railMaterialParams;
    private static CommonMaterialParameters? s_railPointMaterialParams;

    private static Matrix4x4 s_viewMatrix = Matrix4x4.Identity;
    private static Matrix4x4 s_projectionMatrix = Matrix4x4.Identity;
    private static Vector3 s_cameraRotation;
    private static StageLight _defaultLight = new StageLight();

    public static Dictionary<string, TextureSampler> GeneralLUTs = new();

    public static bool VisibleAreas = false;
    public static bool VisibleCameraAreas = true;
    public static bool VisibleRails = true;
    public static bool VisibleGrid = true;
    public static bool VisibleTransparentWall = true;
    public static bool VisibleRelationLines = true;
    
    public static bool UseFullAlphaPipeline = true;

    public static void Initialize(GL gl, LayeredFSHandler fsHandler)
    {
        DefaultCubeRenderer.Initialize(gl);
        AreaRenderer.Initialize(gl);

        s_commonSceneParams = new();

        s_defaultCubeMaterialParams = new(new(1, 0.5f, 0, 1), s_highlightColor);
        s_transparentWallMaterialParams = new(new(0.3f, 0.3f, 0.3f, 1), s_highlightColor);
        s_railGeometryParams = new(lineWidth: 0.08f, camera: new(1));
        s_railHandleGeoParams = new(0.04f, new(1));
        s_railMaterialParams = new(new(0.25f, 0.25f, 0.31f, 1), s_highlightColor);
        s_railPointMaterialParams = new(new(1,0,0.3f, 1), s_highlightColor);
        s_relationMaterialParams = new(new(0.2f, 0.5f, 0.91f, 1), s_highlightColor);
        s_relationParams = new( 0.1f, new(1) );

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
        s_railHandleGeoParams!.Camera = cameraEye;
        s_relationParams!.Camera = cameraEye;
    }

    public static void Draw(GL gl, ISceneObj sceneObj, ReadOnlyDictionary<string, string> CCNT, Scene scn)
    {
        if (s_commonSceneParams is null || s_defaultCubeMaterialParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(Draw)}"
            );

        StageObj? stageObj = null;

        if (sceneObj is IStageSceneObj stageSceneObj) stageObj = stageSceneObj.StageObj;

        if (!sceneObj.IsVisible)
            return;

        if (sceneObj is BasicSceneObj basicSceneObj && stageObj!.IsArea())
        {
            if (!VisibleAreas && !sceneObj.Selected && stageObj.Type != StageObjType.CameraArea)
                return;

            if (!VisibleCameraAreas && !sceneObj.Selected && stageObj.Type == StageObjType.CameraArea)
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

            AreaRenderer.Render(gl, s_commonSceneParams, basicSceneObj.MaterialParams, basicSceneObj.StageObj.Properties.TryGetValue("ShapeModelNo", out object? val) ? (int)val! : 0, sceneObj.PickingId);
            return;
        }

        if (sceneObj is RailSceneObj railSceneObj)
        {
            if(!railSceneObj.RailModel.Initialized)
                return;
            s_railMaterialParams!.Selected = railSceneObj.Selected;

            gl.Disable(EnableCap.CullFace);
            RailRenderer.Render(gl, railSceneObj, railSceneObj.Selected,
                s_commonSceneParams, s_railGeometryParams!, s_railHandleGeoParams!, s_railMaterialParams, s_railPointMaterialParams!);
            return;
        }

        if (sceneObj is ActorSceneObj actorSceneObj)
        {
            Actor actor = actorSceneObj.Actor;

            if (actorSceneObj.StageObj.Parent != null && VisibleRelationLines)
            {
                s_relationMaterialParams!.Selected = sceneObj.Selected;

                gl.CullFace(TriangleFace.Back);
                RelationLine.Render(gl, s_commonSceneParams, s_relationParams!, s_relationMaterialParams, actorSceneObj.PickingId, actorSceneObj.StageObj.Translation + actorSceneObj.DeltaTranslation, actorSceneObj.StageObj.Parent.Translation);
            }

            if (actor.IsEmptyModel)
            {
                s_commonSceneParams.Transform = sceneObj.Transform;
                s_defaultCubeMaterialParams.Selected = sceneObj.Selected;

                gl.CullFace(TriangleFace.Back);

                DefaultCubeRenderer.Render(gl, s_commonSceneParams, s_defaultCubeMaterialParams, sceneObj.PickingId);
                return;
            }

            if (actor.Name.Contains("TransparentWall") && !VisibleTransparentWall)
            {
                return;
            }

            foreach (H3DMeshLayer layer in Enum.GetValues<H3DMeshLayer>())
            {
                List<H3DRenderingMaterial> m = new();
                List<H3DRenderingMesh> l = new();
                int c = 0;
                foreach (var (mesh, material) in actor.EnumerateMeshes(layer))
                {
                    // if (l.Count > 0 && l.Exists(x => x.Priority <= mesh.Priority))// && mesh.Priority != 0)) 
                    // {
                    //     int id = l.FindIndex(x => x.Priority <= mesh.Priority); 
                    //     l.Insert(id, mesh); m.Insert(id, material); 
                    // }
                    // else { l.Add(mesh); m.Add(material); }
                    // c+= 1;
                    m.Add(material);
                    l.Add(mesh);
                }
                // l.Reverse();
                // m.Reverse();
                
                //
            for (int h = 0; h < m.Count; h++)
            {
                var material = m[h];
                var mesh = l[h];
                    material.SetMatrices(s_projectionMatrix, actorSceneObj.Transform, s_viewMatrix);
                material.SetSelectionColor(new(s_highlightColor, actorSceneObj.Selected ? 0.4f : 0));
                if (scn.CanPreviewLights) material.SetLight0((scn.PreviewOneLight ? (scn.PreviewLight ?? scn.GetPreviewLight(actor.InitLight.Type)) : scn.GetPreviewLight(actor.InitLight.Type)) ?? _defaultLight);
                else material.SetLight0(_defaultLight); 
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
                        gl.Enable(EnableCap.Blend);

                        gl.BlendColor(
                            material.BlendingColor.X,
                            material.BlendingColor.Y,
                            material.BlendingColor.Z,
                            material.BlendingColor.W
                        );

                        gl.BlendEquationSeparate(material.ColorBlendEquation, BlendEquationModeEXT.Max);//material.AlphaBlendEquation);

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
                    gl.DepthMask(material.DepthMaskEnabled); // /* Hacky fix to self overlapping alphas (within the same mesh),*/ if we wanted it && !material.Name.Contains("Edge"));

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
//                     if (actor.Name == "Pole") // if the actor modifies the bones
//                         material.ChangeUnivReg(0, 2, 3, actorSceneObj.StageObj.Scale.Y, gl);
                    mesh.Draw();

                    gl.Enable(EnableCap.CullFace);
                    gl.Disable(EnableCap.PolygonOffsetFill);
                    gl.Disable(EnableCap.Blend);
                }
            }
            }
        }
    }

    /// <summary>
    /// Separated from the draw function so it's only drawn once
    /// </summary>
    /// <param name="gl"></param>
    /// <param name="actorSceneObj"></param>
    public static void DrawRelLines(GL gl, ActorSceneObj actorSceneObj)
    {
        if (VisibleRelationLines)
        {
            s_relationMaterialParams!.Selected = actorSceneObj.Selected;

            gl.CullFace(TriangleFace.Back);
            RelationLine.Render(gl, s_commonSceneParams, s_relationParams!, s_relationMaterialParams, actorSceneObj.PickingId, actorSceneObj.StageObj.Translation + actorSceneObj.DeltaTranslation, actorSceneObj.StageObj.Parent.Translation);
        }
    }
    public static void DrawShadow(GL gl, Actor actor, Matrix4x4 Tr, bool Selected, uint PickId, bool fullActor = false)
    {       
            List<H3DRenderingMaterial> m = new();
            List<H3DRenderingMesh> l = new();
            List<(H3DRenderingMaterial, H3DRenderingMesh)> lm = new();
            int c = 0;
            foreach (var (mesh, material) in actor.EnumerateMeshes())
            {
                l.Add(mesh); 
                m.Add(material);
                lm.Add((material, mesh));
                c+= 1;
            }
            // lm.OrderBy(x => x.Item1.Name);
            {
                if (actor.Name.Contains("ShadowVolumeDokan"))
                {
                    lm.Reverse();
                }
                for (int h = 0; h < lm.Count; h++)
                {
                    var material = lm[h].Item1;
                    var mesh = lm[h].Item2;
                    material.SetMatrices(s_projectionMatrix, Tr, s_viewMatrix);
                    material.SetSelectionColor(new(s_highlightColor, Selected ? 0.4f : -1f));

                    if (!material.TryUse(gl, out ProgramUniformScope scope))
                        continue;

                    using (scope)
                    {
                        if (material.CullFaceMode == TriangleFace.FrontAndBack)
                            gl.Disable(EnableCap.CullFace);
                        else
                            gl.CullFace(material.CullFaceMode);

                        gl.Enable(EnableCap.Blend);

                        gl.BlendColor(
                            material.BlendingColor.X,
                            material.BlendingColor.Y,
                            material.BlendingColor.Z,
                            material.BlendingColor.W
                        );

                        gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);//material.AlphaBlendEquation);

                        gl.BlendFuncSeparate(
                            BlendingFactor.SrcColor,
                            BlendingFactor.DstAlpha,
                            BlendingFactor.One,
                            BlendingFactor.One
                        );
                        gl.StencilFunc(material.StencilFunction, material.StencilRef, material.StencilMask);

                        gl.StencilMask(material.StencilBufferMask);

                        gl.StencilOp(material.StencilOps[0], material.StencilOps[1], material.StencilOps[2]);

                        gl.DepthFunc(material.DepthFunction);//h == 0 ? DepthFunction.Greater : DepthFunction.Less);
                        gl.DepthMask(h == 1 ? material.DepthMaskEnabled : false);

                        // gl.ColorMask(
                        //     material.ColorMask[0],
                        //     material.ColorMask[1],
                        //     material.ColorMask[2],
                        //     material.ColorMask[3]
                        // );
                        gl.ColorMask(
                            fullActor,
                            true,
                            true,
                            true
                        );

                        if (material.PolygonOffsetFillEnabled)
                        {
                            gl.Enable(EnableCap.PolygonOffsetFill);
                            gl.PolygonOffset(0, material.PolygonOffsetUnit);
                        }
                        
                        material.Program.TryGetUniformLoc("uPickingId", out int location);
                        gl.Uniform1(location, PickId);
                        material.Program.TryGetUniformLoc("uShadow", out int location2);
                        gl.Uniform1(location2, h == 0 ? 1u : 2u);


                        mesh.Draw();

                        gl.Enable(EnableCap.CullFace);
                        gl.Disable(EnableCap.PolygonOffsetFill);
                        gl.Disable(EnableCap.Blend);
                        gl.ColorMask(
                            true,
                            true,
                            true,
                            true
                        );
                    }
                }
            }
    }

    public static void DrawLayer(GL gl, ISceneObj sceneObj, Scene scn, H3DMeshLayer layer)
    {
        if (s_commonSceneParams is null || s_defaultCubeMaterialParams is null)
            throw new InvalidOperationException(
                $@"{nameof(ModelRenderer)} must be initialized before any calls to {nameof(Draw)}"
            );
            
        if (!sceneObj.IsVisible)
            return;

        if (layer == H3DMeshLayer.Opaque)
        {
            StageObj? stageObj = null;

            if (sceneObj is IStageSceneObj stageSceneObj) stageObj = stageSceneObj.StageObj;


            if (sceneObj is BasicSceneObj basicSceneObj && stageObj!.IsArea())
            {
                if (!VisibleAreas && !sceneObj.Selected && stageObj.Type != StageObjType.CameraArea)
                    return;

                if (!VisibleCameraAreas && !sceneObj.Selected && stageObj.Type == StageObjType.CameraArea)
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

                AreaRenderer.Render(gl, s_commonSceneParams, basicSceneObj.MaterialParams, basicSceneObj.StageObj.Properties.TryGetValue("ShapeModelNo", out object? val) ? (int)val! : 0, sceneObj.PickingId);
                return;
            }

            if (sceneObj is RailSceneObj railSceneObj)
            {
                if(!railSceneObj.RailModel.Initialized)
                    return;
                s_railMaterialParams!.Selected = railSceneObj.Selected;

                gl.Disable(EnableCap.CullFace);
                RailRenderer.Render(gl, railSceneObj, railSceneObj.Selected,
                    s_commonSceneParams, s_railGeometryParams!, s_railHandleGeoParams!, s_railMaterialParams, s_railPointMaterialParams!);
                return;
            }
            if (sceneObj is ActorSceneObj actorSceneObj1)
            {
                Actor actor = actorSceneObj1.Actor;
                if (actor.IsEmptyModel)
                {
                    s_commonSceneParams.Transform = sceneObj.Transform;
                    s_defaultCubeMaterialParams.Selected = sceneObj.Selected;

                    gl.CullFace(TriangleFace.Back);

                    DefaultCubeRenderer.Render(gl, s_commonSceneParams, s_defaultCubeMaterialParams, sceneObj.PickingId);
                    return;
                }

                if (actor.Name.Contains("TransparentWall"))
                {
                    if (!VisibleTransparentWall && !sceneObj.Selected)
                        return;
                    s_commonSceneParams.Transform = sceneObj.Transform;
                    s_transparentWallMaterialParams!.Selected = sceneObj.Selected;

                    TransparentWallRenderer.Render(gl, s_commonSceneParams, s_transparentWallMaterialParams, sceneObj.PickingId);
                    return;
                }
            }
        }

        if (sceneObj is ActorSceneObj actorSceneObj)
        {
            Actor actor = actorSceneObj.Actor;
            
            List<H3DRenderingMaterial> m = new();
            List<H3DRenderingMesh> l = new();
            // var d = actor.EnumerateMeshes(layer).ToDictionary();
            // foreach (var (mesh, material) in actor.EnumerateMeshes(layer))
            // {
            //     if (l.Count > 0 && mesh.Priority < l[0].Priority) { l.Insert(0, mesh); m.Insert(0, material); }
            //     else { l.Add(mesh); m.Add(material); }
            // }
            // Console.WriteLine("////////////");
            int c = 0;
            List<(H3DRenderingMesh, H3DRenderingMaterial)> dd;
            if (layer == H3DMeshLayer.Opaque)
                dd = actor.EnumerateMeshes(layer).ToList();
            else
            {
                dd = actor.EnumerateMeshes(layer).ToList();
                //dd = actor.MeshesUnlayered.ToList();
                dd.Reverse();
            }
            foreach (var (mesh, material) in dd)
            {
                if (l.Count > 0 && l.Exists(x => x.Priority <= mesh.Priority))// && mesh.Priority != 0)) 
                {
                    int id = -1;
                    if (l.Exists(x => x.Priority < mesh.Priority))
                        id = l.FindIndex(x => x.Priority < mesh.Priority);
                    else if (l.Exists(x => x.Priority == mesh.Priority))
                        id = l.FindLastIndex(x => x.Priority == mesh.Priority) + 1;
                    l.Insert(id, mesh); m.Insert(id, material);
                }
                // else if (l.Count > 0  && l.Exists(x => x.Priority == 0 && mesh.Priority == 0))
                // {
                //     int id = l.FindLastIndex(x => x.Priority == 0); 
                //     l.Insert(id, mesh); m.Insert(id, material); 
                // }
                // else if (l.Count > 0)
                // {
                //     if (Vector3.Distance(mesh.Center + actorSceneObj.StageObj.Translation, scn.Camera.Eye * 100) < Vector3.Distance(l[c].Center + actorSceneObj.StageObj.Translation, scn.Camera.Eye * 100))
                //     {
                //         l.Insert(l.Count-1, mesh); m.Insert(m.Count-1, material);
                //     }
                // }
                else { l.Add(mesh); m.Add(material); }
                // Console.WriteLine(l[c].Center);
                c+= 1;
            }
            l.Reverse();
            m.Reverse();
            // Console.WriteLine(actorSceneObj.StageObj.Translation);
            // Console.WriteLine(scn.Camera.Eye);
            # warning THIS WILL RENDER MULTIPLE TIMES IF AN OBJECT HAS MULTIPLE LAYERS
            if (actorSceneObj.SubActors.Count > 0)
            {
                int cnt = 0;
                foreach (Actor SubActor in actorSceneObj.SubActors)
                {
                    DrawSubActor(gl, actorSceneObj, SubActor, cnt, scn);
                    cnt += 1;
                } 
            }

            for (int h = 0; h < m.Count; h++)
            {
                var material = m[h];
                var mesh = l[h];
                material.SetMatrices(s_projectionMatrix, actorSceneObj.Transform, s_viewMatrix);
                material.SetSelectionColor(new(s_highlightColor, actorSceneObj.Selected ? 0.4f : 0));
                if (scn.CanPreviewLights) material.SetLight0((scn.PreviewOneLight ? (scn.PreviewLight ?? scn.GetPreviewLight(actor.InitLight.Type)) : scn.GetPreviewLight(actor.InitLight.Type)) ?? _defaultLight);
                else material.SetLight0(_defaultLight); 
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
                        gl.Enable(EnableCap.Blend);

                        gl.BlendColor(
                            material.BlendingColor.X,
                            material.BlendingColor.Y,
                            material.BlendingColor.Z,
                            material.BlendingColor.W
                        );

                        gl.BlendEquationSeparate(material.ColorBlendEquation, BlendEquationModeEXT.Max);//material.AlphaBlendEquation);

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
                    gl.DepthMask(material.DepthMaskEnabled); // /* Hacky fix to self overlapping alphas (within the same mesh),*/ if we wanted it && !material.Name.Contains("Edge"));

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

    public static void DrawSubActor(GL gl, ActorSceneObj actorSceneObj, Actor act, int idx, Scene scn)
    {
        foreach (var (mesh, material) in act.EnumerateMeshes())
        {
            string className = actorSceneObj.StageObj.Name;
            material.SetMatrices(
                s_projectionMatrix, 
                Matrix4x4.CreateRotationX(actorSceneObj.SubActorTransforms[idx].Rotate.X * MathF.PI / 180) *
                Matrix4x4.CreateRotationY(actorSceneObj.SubActorTransforms[idx].Rotate.Y * MathF.PI / 180) *
                Matrix4x4.CreateRotationZ(actorSceneObj.SubActorTransforms[idx].Rotate.Z * MathF.PI / 180) *
                MathUtils.CreateTransformWithDelta((actorSceneObj.StageObj.Translation) * 0.01f, 
                (actorSceneObj.DeltaTranslation + actorSceneObj.SubActorTransforms[idx].Translate * actorSceneObj.StageObj.Scale) * 0.01f,
                actorSceneObj.StageObj.Scale * actorSceneObj.SubActorTransforms[idx].Scale * 0.01f, 
                actorSceneObj.StageObj.Rotation+ actorSceneObj.DeltaRotation), 
                s_viewMatrix);

            material.SetSelectionColor(new(s_highlightColor, actorSceneObj.Selected ? 0.4f : 0));
            if (scn.CanPreviewLights) material.SetLight0((scn.PreviewOneLight ? (scn.PreviewLight ?? scn.GetPreviewLight(act.InitLight.Type)) : scn.GetPreviewLight(act.InitLight.Type)) ?? _defaultLight);
            else material.SetLight0(_defaultLight); 
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
                    gl.Enable(EnableCap.Blend);

                    gl.BlendColor(
                        material.BlendingColor.X,
                        material.BlendingColor.Y,
                        material.BlendingColor.Z,
                        material.BlendingColor.W
                    );

                    gl.BlendEquationSeparate(material.ColorBlendEquation, BlendEquationModeEXT.Max );//material.AlphaBlendEquation);

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
                gl.DepthMask(material.DepthMaskEnabled); // /* Hacky fix to self overlapping alphas (within the same mesh),*/ if we wanted it && !material.Name.Contains("Edge"));

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
