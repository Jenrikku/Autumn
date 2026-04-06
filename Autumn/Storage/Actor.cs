using System.Text;
using Autumn.Enums;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.CtrH3D.Animation;
using Autumn.Rendering.Storage;
using BYAMLSharp;
using NARCSharp;
using SceneGL.GLHelpers;
using SceneGL.Materials.Common;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;

namespace Autumn.Storage;

internal class Actor
{
    public string Name { get; private set; }
    public bool IsEmptyModel { get; private set; }
    public bool IsShadowModel { get; set; } = false;

    public AxisAlignedBoundingBox AABB { get; set; } = new(2);
    public ActorLight InitLight = new();
    public List<ActorShadow> InitShadow = new();
    public ActorInfo Info;

    /// <summary>
    /// An array of mesh lists. Each entry in the array represents a mesh layer.
    /// </summary>
    /// <seealso cref="H3DMeshLayer"/>
    private readonly List<(H3DRenderingMesh, H3DRenderingMaterial)>[] _meshes;

    // Note: in order to make a TextureSampler we need an H3DTextureMapper
    // which depends on the material.
    private readonly Dictionary<string, uint> _textures;

    private readonly Dictionary<string, TextureSampler> _lutSamplers;

    public Actor(string name)
    {
        Name = name;
        IsEmptyModel = true;

        _meshes = new List<(H3DRenderingMesh, H3DRenderingMaterial)>[4];

        for (int i = 0; i < _meshes.Length; i++)
            _meshes[i] = new();

        _textures = new();
        _lutSamplers = new();
    }

    /// <summary>
    /// Adds a mesh to the actor.<br/>
    /// Make sure to add the textures first.
    /// </summary>
    public void AddMesh(
        GL gl,
        H3DMeshLayer layer,
        H3DMesh mesh,
        H3DSubMeshCulling? subMeshCulling,
        H3DMaterial material,
        H3DDict<H3DBone> skeleton
    )
    {
        H3DSkeletalAnimator animator = new(skeleton);

        H3DRenderingMesh renderingMesh = new(gl, mesh, subMeshCulling);
        H3DRenderingMaterial renderingMaterial = new(gl, material, mesh, animator, this);

        _meshes[(int)layer].Add((renderingMesh, renderingMaterial));

        IsEmptyModel = false;
    }

    public void AddTexture(GL gl, H3DTexture texture)
    {
        if (_textures.ContainsKey(texture.Name)) return;
        byte[] textureData = texture.ToRGBA();

        uint glTexture = TextureHelper.CreateTexture2D<byte>(
            gl,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm,
            (uint)texture.Width,
            (uint)texture.Height,
            textureData,
            true
        );

        _textures.Add(texture.Name, glTexture);
    }

    public void AddLUTTexture(GL gl, string tableName, H3DLUTSampler sampler)
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

        _lutSamplers.Add(name, textureSampler);
    }

    public uint GetTexture(GL gl, string name)
    {
        if (!_textures.TryGetValue(name, out uint result))
        {
            // Default to black texture when it does not exist.
            return TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.BLACK);
        }

        return result;
    }

    public TextureSampler GetLUTTexture(GL gl, string tableName, string samplerName)
    {
        if (!_lutSamplers.TryGetValue(tableName + samplerName, out TextureSampler result))
        {
            if (!ModelRenderer.GeneralLUTs.TryGetValue(tableName + samplerName, out result))
            {
                // Default values for non-existing luts.
                uint sampler = SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.NEAREST);
                uint texture = TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.BLACK);

                return new(sampler, texture);
            }
        }

        return result;
    }

    public bool TryGetLUTTexture(string tableName, string samplerName, out TextureSampler result)
    {
        if (!_lutSamplers.TryGetValue(tableName + samplerName, out result))
        {
            return ModelRenderer.GeneralLUTs.TryGetValue(tableName + samplerName, out result);
        }
        return true;
    }

    public IEnumerable<(H3DRenderingMesh Mesh, H3DRenderingMaterial Material)> EnumerateMeshes(H3DMeshLayer layer)
    {
        foreach (var tuple in _meshes[(int)layer])
            yield return tuple;
    }
    public IEnumerable<(H3DRenderingMesh Mesh, H3DRenderingMaterial Material)> EnumerateMeshes()
    {
        for (int layer = 0; layer < 4; layer++)
        foreach (var tuple in _meshes[(int)layer])
            yield return tuple;
    }

    public int CountMeshesLayer(H3DMeshLayer l) => _meshes[(int)l].Count;

    public void ForceModelNotEmpty() => IsEmptyModel = false;


    public void ReadActorInits(NARCFileSystem narc, Encoding enc)
    {
        if (narc.TryGetFile("InitLight.byml", out byte[] light))
        {
            BYAML act_lights = BYAMLParser.Read(light, enc);
            if (act_lights.RootNode.NodeType == BYAMLNodeType.Dictionary)
            {
                var lrt = act_lights.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>();
                if (lrt!.ContainsKey("LightCalcType"))
                    InitLight.GetCalcType((string)lrt["LightCalcType"].Value!);
                if (lrt!.ContainsKey("LightType"))
                    InitLight.GetType((string)lrt["LightType"].Value!);
            }
        }
        if (narc.TryGetFile("InitShadow.byml", out byte[] shadows))
        {
            BYAML act_sh = BYAMLParser.Read(shadows, enc);
            if (act_sh.RootNode.NodeType == BYAMLNodeType.Dictionary)
            {
                var srt = act_sh.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>();
                if (srt!.ContainsKey("Category") && srt["Category"].Value != null)
                {
                    if (srt["Category"].NodeType == BYAMLNodeType.String && !string.IsNullOrEmpty((string)srt["Category"].Value!))
                        Console.WriteLine($"{Name} Shadows Contains actual category");
                }
                if (srt!.ContainsKey("Shadows"))
                {
                    //Console.WriteLine("Contains Shadows");
                    var shArr = srt["Shadows"];
                    if (shArr.NodeType == BYAMLNodeType.Array)
                    {
                        BYAMLNode[] shs = shArr.GetValueAs<BYAMLNode[]>()!;
                        foreach (BYAMLNode shd in shs)
                        {
                            if (shd.NodeType is not BYAMLNodeType.Dictionary) continue;
                            InitShadow.Add(new (shd.GetValueAs<Dictionary<string, BYAMLNode>>()!));
                        }
                    }
                    //srt.TryGetValue("", out BYAMLNode SName)
                }
            }
        }
        if (narc.TryGetFile("InitActor.byml", out byte[] actInfo))
        {
            BYAML act_inf = BYAMLParser.Read(actInfo, enc);
            if (act_inf.RootNode.NodeType == BYAMLNodeType.Dictionary)
            {
                Info = new();
                var srt = act_inf.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>();
                if (srt!.ContainsKey("MaterialController"))
                {
                    // Info.ProjectionYOffset = ;
                    var a = srt["MaterialController"].GetValueAs<Dictionary<string, BYAMLNode>>();
                    if (a != null)
                    {
                        if (a.ContainsKey("ProjTex1dLocalTransY"))
                        {
                            if (a["ProjTex1dLocalTransY"].Value != null)
                            {
                                var b = a["ProjTex1dLocalTransY"].GetValueAs<Dictionary<string, BYAMLNode>>()!;
                                Info.ProjectionYOffset = b["Offset"].GetValueAs<float>();
                                if (b.ContainsKey("HeightScale")) Info.ProjectionYOffset *= b["HeightScale"].GetValueAs<float>();
                            }
                        }
                        else if (a.ContainsKey("ProjTex1dRelTransY"))
                        {
                            if (a["ProjTex1dRelTransY"].Value != null)
                            {
                                var b = a["ProjTex1dRelTransY"].GetValueAs<Dictionary<string, BYAMLNode>>()!;
                                Info.ProjectionYOffset = b["Offset"].GetValueAs<float>();
                                if (b.ContainsKey("HeightScale")) Info.ProjectionYOffset *= b["HeightScale"].GetValueAs<float>();
                            }
                        }
                    }
                }
                if (srt!.ContainsKey("UseMaterialFogEnableFlag"))
                {
                    Info.UseFog = false;
                }
            }
        }
    }


}
