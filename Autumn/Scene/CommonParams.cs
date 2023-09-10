using System.Numerics;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Scene;

internal sealed class CommonSceneParameters
{
    private readonly UniformBuffer<SceneData> _buffer;
    internal ShaderParams ShaderParameters { get; }

    public struct SceneData
    {
        public Matrix4x4 ViewProjection;
        public Matrix4x4 Transform;
    }

    public CommonSceneParameters(GL gl) =>
        ShaderParameters = ShaderParams.FromUniformBlockDataAndSamplers(
            "ubScene",
            new SceneData(),
            "ubSceneBuffer",
            Array.Empty<SamplerBinding>(),
            out _buffer
        );

    public Matrix4x4 ViewProjection
    {
        get => _buffer.Data.ViewProjection;
        set => _buffer.SetData(_buffer.Data with { ViewProjection = value });
    }

    public Matrix4x4 Transform
    {
        get => _buffer.Data.Transform;
        set => _buffer.SetData(_buffer.Data with { Transform = value });
    }
}

internal sealed class CommonMaterialParameters
{
    private readonly UniformBuffer<MaterialData> _buffer;
    internal ShaderParams ShaderParameters { get; }

    public struct MaterialData
    {
        public Vector4 Color;
        public Vector3 HighlightColor;
        public float HighlightAlpha;
    }

    public CommonMaterialParameters(GL gl, Vector4 color, Vector3 highlightColor) =>
        ShaderParameters = ShaderParams.FromUniformBlockDataAndSamplers(
            "ubMaterial",
            new MaterialData() { Color = color, HighlightColor = highlightColor },
            "ubMaterialBuffer",
            Array.Empty<SamplerBinding>(),
            out _buffer
        );

    public Vector4 Color
    {
        get => _buffer.Data.Color;
        set => _buffer.SetData(_buffer.Data with { Color = value });
    }

    public Vector3 HighlightColor
    {
        get => _buffer.Data.HighlightColor;
        set => _buffer.SetData(_buffer.Data with { HighlightColor = value });
    }

    public bool Selected
    {
        get => _buffer.Data.HighlightAlpha > 0;
        set => _buffer.SetData(_buffer.Data with { HighlightAlpha = value ? 0.8f : 0 });
    }
}
