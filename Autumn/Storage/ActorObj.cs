using Autumn.Scene.H3D;
using Autumn.Scene.H3D.Animation;
using SceneGL;
using Silk.NET.Maths;
using SPICA.Formats.CtrH3D.Animation;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;

namespace Autumn.Storage;

internal class ActorObj
{
    public struct RGBATexture
    {
        public uint Width;
        public uint Height;

        public byte[] Data;
    }

    public H3DRenderingMaterial[] RenderingMaterials { get; set; }
    public RenderableModel[] RenderableModels { get; set; }
    public Dictionary<string, RGBATexture> RGBATextures { get; set; }
    public Dictionary<string, float[]> LUTSamplerTextures { get; set; }

    public H3DSkeletalAnimator? SkeletalAnimator { get; set; }

    public string Name { get; set; }
    public bool IsNoModel { get; set; }

    public List<(string Name, H3DMesh Mesh)> Meshes { get; }
    public List<H3DMaterial> Materials { get; }
    public List<H3DBone> Skeleton { get; }

    public List<H3DTexture> Textures { get; }

    public List<H3DAnimation> SkeletalAnimations { get; }
    public List<H3DAnimation> MaterialAnimations { get; }
    public List<H3DAnimation> VisibilityAnimations { get; }
    public List<H3DAnimation> LightAnimations { get; }
    public List<H3DAnimation> CameraAnimations { get; }
    public List<H3DAnimation> FogAnimations { get; }

    public List<(string Name, H3DLUTSampler Sampler)> LUTSamplers { get; }

    public ActorObj(string name, bool isEmpty = false)
    {
        RenderingMaterials = Array.Empty<H3DRenderingMaterial>();
        RenderableModels = Array.Empty<RenderableModel>();
        RGBATextures = new();
        LUTSamplerTextures = new();

        Name = name;
        IsNoModel = isEmpty;

        Meshes = new(0);
        Materials = new(0);
        Skeleton = new(0);
        Textures = new(0);

        SkeletalAnimations = new(0);
        MaterialAnimations = new(0);
        VisibilityAnimations = new(0);
        LightAnimations = new(0);
        CameraAnimations = new(0);
        FogAnimations = new(0);

        LUTSamplers = new(0);
    }
}
