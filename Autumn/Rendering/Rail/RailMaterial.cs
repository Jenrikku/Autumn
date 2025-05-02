using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal static class RailMaterial
{

    private static readonly ShaderSource s_vertexShader =
        new(
            "Rail.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            void main() {
                gl_Position = uViewProjection * uTransform * vec4(aPos, 1.0);
            }
            """
        );

    private static readonly ShaderSource s_geometryShader =
        new(
            "Rail.geom",
            ShaderType.GeometryShader,
            """
            #version 330

            layout(lines) in;
            layout(triangle_strip, max_vertices = 4) out;

            layout(std140) uniform ubGeometry {
                float uWidth;
                vec2 uViewport;
            };

            vec2 toScreenSpace(vec4 vertex) {
                return vec2(vertex.xy / vertex.w) * uViewport;
            }

            float toZValue(vec4 vertex) {
                return (vertex.z / vertex.w);
            }

            void main() {
                vec2 p0 = toScreenSpace(gl_in[0].gl_Position);
                vec2 p1 = toScreenSpace(gl_in[1].gl_Position);
                float z0 = toZValue(gl_in[0].gl_Position);
                float z1 = toZValue(gl_in[1].gl_Position);

                gl_Position = vec4((p0 + uWidth) / uViewport, z0, 1.0);
                EmitVertex();

                gl_Position = vec4((p0 - uWidth) / uViewport, z0, 1.0);
                EmitVertex();

                gl_Position = vec4((p1 + uWidth) / uViewport, z1, 1.0);
                EmitVertex();

                gl_Position = vec4((p1 - uWidth) / uViewport, z1, 1.0);
                EmitVertex();

                EndPrimitive();
            }
            """
        );

    private static readonly ShaderSource s_fragmentShader =
        new(
            "Rail.frag",
            ShaderType.FragmentShader,
            """
            #version 330

            layout(std140) uniform ubMaterial {
                vec4 uColor;
                vec4 uHighlightColor;
            };

            uniform uint uPickingId;

            out vec4 oColor;
            out uint oPickingId;

            void main() {
                oPickingId = uPickingId;

                oColor = uColor;
                oColor.rgb = mix(oColor.rgb, uHighlightColor.rgb, uHighlightColor.a);
            }
            """
        );

    public static readonly ShaderProgram Program = new(s_vertexShader, s_geometryShader, s_fragmentShader);

    public static bool TryUse(
        GL gl,
        CommonSceneParameters scene,
        RailGeometryParameters geometry,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) => Program.TryUse(gl, null, [scene.ShaderParameters, geometry.ShaderParameters, material.ShaderParameters], out scope, out _);
}

internal sealed class RailGeometryParameters
{
    private readonly UniformBuffer<GeometryData> _buffer;
    internal ShaderParams ShaderParameters { get; }

    public struct GeometryData
    {
        public float LineWidth;
        private int _padding;
        public Vector2 Viewport;
    }

    public RailGeometryParameters(float lineWidth, Vector2 viewport) =>
        ShaderParameters = ShaderParams.FromUniformBlockDataAndSamplers(
            "ubGeometry",
            new GeometryData() { LineWidth = lineWidth, Viewport = viewport },
            "ubGeometryBuffer",
            [],
            out _buffer
        );

    public float LineWidth
    {
        get => _buffer.Data.LineWidth;
        set => _buffer.SetData(_buffer.Data with { LineWidth = value });
    }

    public Vector2 Viewport
    {
        get => _buffer.Data.Viewport;
        set => _buffer.SetData(_buffer.Data with { Viewport = value });
    }
}
