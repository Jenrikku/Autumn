using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using NARCSharp;
using Silk.NET.Maths;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Animation;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;

namespace Autumn.IO;

internal static class ObjectHandler
{
    private static readonly Dictionary<string, ActorObj> s_cachedRomFSObjects = new();

    /// <returns>An <see cref="ActorObj"/> from either the loaded project or the provided RomFS.</returns>
    public static ActorObj GetObject(string name, string? modelName = null)
    {
        if (s_cachedRomFSObjects.TryGetValue(name, out ActorObj? obj))
            return obj;
        else
        {
            if (ProjectHandler.Objects.TryGetValue(name, out obj))
                return obj;

            if (!TryImportObject(name, modelName, out obj))
                obj = new(name, true);

            s_cachedRomFSObjects.Add(name, obj);

            return obj;
        }
    }

    public static bool TryImportObject(string name, string? modelName, out ActorObj obj) =>
        TryImportObjectFrom(
            Path.Join(RomFSHandler.RomFSPath, "ObjectData"),
            modelName ?? name,
            out obj
        );

    public static bool TryImportObjectFrom(string directory, string name, out ActorObj obj)
    {
        obj = new(name);

        string path = Path.Join(directory, name + ".szs");

        if (!File.Exists(path) || !SZSWrapper.TryReadFile(path, out NARCFileSystem? narc))
            return false;

        byte[] bcmdl = narc.GetFile(name + ".bcmdl");

        if (bcmdl.Length == 0)
            return false;

        H3D h3D;

        try
        {
            using MemoryStream stream = new(bcmdl);
            h3D = Gfx.OpenAsH3D(stream);
        }
        catch
        {
            return false;
        }

        foreach (H3DModel model in h3D.Models)
        {
            foreach (H3DMesh mesh in model.Meshes)
            {
                string meshName;

                if (model.MeshNodesTree?.Count > 0 && mesh.NodeIndex < model.MeshNodesTree.Count)
                    meshName = model.MeshNodesTree.Find(mesh.NodeIndex);
                else
                    meshName = model.Name + model.Meshes.IndexOf(mesh);

                obj.Meshes.Add((meshName, mesh));
            }

            if (obj.Meshes.Count <= 0)
                obj.IsNoModel = true;

            foreach (H3DMaterial material in model.Materials)
                obj.Materials.Add(material);

            foreach (H3DBone bone in model.Skeleton)
                obj.Skeleton.Add(bone);

            if (model.Skeleton.Count > 0)
                obj.SkeletalAnimator = new(model.Skeleton);
        }

        foreach (H3DTexture texture in h3D.Textures)
        {
            obj.Textures.Add(texture);

            ActorObj.RGBATexture rgbaTexture =
                new()
                {
                    Width = (uint)texture.Width,
                    Height = (uint)texture.Height,
                    Data = texture.ToRGBA()
                };

            obj.RGBATextures.Add(texture.Name, rgbaTexture);
        }

        foreach (H3DAnimation animation in h3D.SkeletalAnimations)
            obj.SkeletalAnimations.Add(animation);

        foreach (H3DAnimation animation in h3D.MaterialAnimations)
            obj.MaterialAnimations.Add(animation);

        foreach (H3DAnimation animation in h3D.VisibilityAnimations)
            obj.VisibilityAnimations.Add(animation);

        foreach (H3DAnimation animation in h3D.LightAnimations)
            obj.LightAnimations.Add(animation);

        foreach (H3DAnimation animation in h3D.CameraAnimations)
            obj.CameraAnimations.Add(animation);

        foreach (H3DAnimation animation in h3D.FogAnimations)
            obj.FogAnimations.Add(animation);

        foreach (H3DLUT lut in h3D.LUTs)
        foreach (H3DLUTSampler sampler in lut.Samplers)
        {
            string lutSamplerName = lut.Name + "/" + sampler.Name;
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

            obj.LUTSamplers.Add((lutSamplerName, sampler));
            obj.LUTSamplerTextures.Add(lutSamplerName, table);
        }

        return true;
    }

    public static void SaveObjectToProject(string directory, string name, ActorObj? obj = null) =>
        SaveObjectToProject(Path.Join(RomFSHandler.RomFSPath, "ObjectData"), name, obj);

    public static void SaveObjectToProject(string name, ActorObj? obj = null)
    {
        if (!ProjectHandler.ProjectLoaded)
            return;

        string? path = ProjectHandler.ProjectSavePath;

        if (string.IsNullOrEmpty(path))
            return;

        path = Directory.CreateDirectory(Path.Join(path, "objects", name)).FullName;

        // Convert to COLLADA (dae).


        // Save all byaml as yaml.


        // Save all textures as png.


        // Generate texform.yml from a dictionary of string (tex name), PICATextureFormat.
        // Export all materials as yaml.
        // Export all LUTs as yaml or png.
    }

    public static ActorObj LoadObjectFromProject(string name)
    {
        ActorObj obj = new(name, true);

        if (!ProjectHandler.DirectoryExists(Path.Join("objects", name)))
            return obj;

        // If dae exists, change empty. May accept other formats.

        // TO-DO.

        return obj;
    }
}
