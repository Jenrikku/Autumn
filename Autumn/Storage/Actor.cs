using Autumn.Enums;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.CtrH3D.Animation;
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

    public AxisAlignedBoundingBox AABB = new AxisAlignedBoundingBox();

    public void BoundBox(H3DBoundingBox Box)
    {
        AABB.Min.X = Math.Min(AABB.Min.X, -Box.Size.X);
        AABB.Max.X = Math.Max(AABB.Max.X, Box.Size.X);
        AABB.Min.Y = Math.Min(AABB.Min.Y, -Box.Size.Y);
        AABB.Max.Y = Math.Max(AABB.Max.Y, Box.Size.Y);
        AABB.Min.Z = Math.Min(AABB.Min.Z, -Box.Size.Z);
        AABB.Max.Z = Math.Max(AABB.Max.Z, Box.Size.Z);
    }

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
            // Default values for non-existing luts.
            uint sampler = SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.NEAREST);
            uint texture = TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.BLACK);

            return new(sampler, texture);
        }

        return result;
    }

    public bool TryGetLUTTexture(string tableName, string samplerName, out TextureSampler result) =>
        _lutSamplers.TryGetValue(tableName + samplerName, out result);

    public IEnumerable<(H3DRenderingMesh Mesh, H3DRenderingMaterial Material)> EnumerateMeshes(H3DMeshLayer layer)
    {
        foreach (var tuple in _meshes[(int)layer])
            yield return tuple;
    }
}
