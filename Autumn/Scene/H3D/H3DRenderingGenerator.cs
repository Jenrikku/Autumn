using System.Numerics;
using Autumn.Storage;
using SceneGL;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.PICA.Commands;
using SPICA.PICA.Converters;

namespace Autumn.Scene.H3D;

internal static class H3DRenderingGenerator
{
    private struct Vertex
    {
        [VertexAttribute(AttributeShaderLoc.Loc0, 3, VertexAttribPointerType.Float, false)]
        public Vector4 Position;

        [VertexAttribute(AttributeShaderLoc.Loc1, 3, VertexAttribPointerType.Float, false)]
        public Vector4 Normal;

        [VertexAttribute(AttributeShaderLoc.Loc2, 4, VertexAttribPointerType.Float, false)]
        public Vector4 Tangent;

        [VertexAttribute(AttributeShaderLoc.Loc3, 4, VertexAttribPointerType.Float, false)]
        public Vector4 Color;

        [VertexAttribute(AttributeShaderLoc.Loc4, 2, VertexAttribPointerType.Float, false)]
        public Vector4 TexCoord0;

        [VertexAttribute(AttributeShaderLoc.Loc5, 2, VertexAttribPointerType.Float, false)]
        public Vector4 TexCoord1;

        [VertexAttribute(AttributeShaderLoc.Loc6, 2, VertexAttribPointerType.Float, false)]
        public Vector4 TexCoord2;

        [VertexAttribute(AttributeShaderLoc.Loc7, 2, VertexAttribPointerType.Int, false)]
        public BoneIndices Indices;

        [VertexAttribute(AttributeShaderLoc.Loc8, 2, VertexAttribPointerType.Float, false)]
        public Vector4 Weights;
    }

    public static unsafe void GenerateMaterialsAndModels(GL gl, ActorObj actorObj)
    {
        int meshCount = actorObj.Meshes.Count;

        if (meshCount <= 0)
            return;

        List<H3DRenderingMaterial> renderingMaterials = new();
        List<(RenderableModel Model, int Layer)> renderableModels = new();

        for (int i = 0; i < meshCount; i++)
        {
            H3DMesh mesh = actorObj.Meshes[i].Mesh;
            H3DMaterial material = actorObj.Materials[mesh.MaterialIndex];

            PICAVertex[] picaVertices = mesh.GetVertices();

            Span<Vertex> vertices;

            fixed (void* vertptr = picaVertices)
                vertices = new((Vertex*)vertptr, picaVertices.Length);

            var colorAttribute = mesh.Attributes.Find(x => x.Name == PICAAttributeName.Color);
            int colorMultiplier = colorAttribute.Format == PICAAttributeFormat.Byte ? 127 : 255;

            for (int j = 0; j < vertices.Length; j++)
                vertices[j] = vertices[j] with
                {
                    Color = vertices[j].Color * colorMultiplier,
                    Weights = vertices[j].Weights * 100
                };

            var renderingMaterial = new H3DRenderingMaterial(gl, material, mesh, actorObj);
            var renderingModel = RenderableModel.Create<ushort, Vertex>(
                gl,
                mesh.SubMeshes[0].Indices,
                vertices
            );

            renderingMaterials.Add(renderingMaterial);
            renderableModels.Add((renderingModel, mesh.Layer));
        }

        renderingMaterials.Sort((first, second) => first.Layer.CompareTo(second.Layer));
        renderableModels.Sort((first, second) => first.Layer.CompareTo(second.Layer));

        actorObj.RenderingMaterials = renderingMaterials.ToArray();
        actorObj.RenderableModels = renderableModels.Select(x => x.Model).ToArray();
    }
}
