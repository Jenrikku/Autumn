using System.Numerics;
using Autumn.Rendering.Storage;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class RelationLineMaterial
{

    private static readonly ShaderSource s_vertexShader =
        new(
            "RLine.vert",
            ShaderType.VertexShader,
            """
            #version 330

            layout(location = 0) in vec3 aPos;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            layout(std140) uniform ubPos {
                uniform vec3 PosA;
                uniform vec3 PosB;
            };


            void main() {
                 
                gl_Position = uTransform * (vec4((gl_VertexID == 0 ? PosA : PosB) * 0.01f, 1.0) * vec4(aPos, 1.0));
            }
            """
        );

    private static readonly ShaderSource s_geometryShader =
        new(
            "RLine.geom",
            ShaderType.GeometryShader,
            """
            #version 330

            layout(lines) in;
            layout(triangle_strip, max_vertices = 4) out;

            layout(std140) uniform ubScene {
                mat4x4 uViewProjection;
                mat4x4 uTransform;
            };

            layout(std140) uniform ubGeometry {
                float uWidth;
                vec3 camera;
            };

            void main() {
                vec3 p0 = gl_in[0].gl_Position.xyz;
                vec3 p1 = gl_in[1].gl_Position.xyz;

                vec3 line = p1 - p0;

                vec3 middle = (p0 + p1) / 2;
                vec3 normal = camera - middle;

                vec3 c = normalize(cross(normal, line));

                gl_Position = uViewProjection * vec4(p0 + c * uWidth, 1.0);
                EmitVertex();

                gl_Position = uViewProjection * vec4(p0 - c * uWidth, 1.0);
                EmitVertex();

                gl_Position = uViewProjection * vec4(p1 + c * uWidth, 1.0);
                EmitVertex();

                gl_Position = uViewProjection * vec4(p1 - c * uWidth, 1.0);
                EmitVertex();

                EndPrimitive();
            }
            """
        );

    private static readonly ShaderSource s_fragmentShader =
        new(
            "RLine.frag",
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
        RelationLineParams rlParams,
        CommonMaterialParameters material,
        out ProgramUniformScope scope
    ) => Program.TryUse(gl, null, [scene.ShaderParameters, rlParams.VertParams, rlParams.GeoParams, material.ShaderParameters], out scope, out _);
}

internal sealed class RelationLineParams
{
    private readonly UniformBuffer<GeometryData> _geobuffer;
    private readonly UniformBuffer<VertexData> _vertbuffer;
    internal ShaderParams GeoParams { get; }
    internal ShaderParams VertParams { get; }

    public struct GeometryData
    {
        public float LineWidth;
        private Vector3 _padding0;
        public Vector3 Camera;
        private float _padding1;
    }
    public struct VertexData
    {
        public Vector3 PosA;
        private float _padding0;
        public Vector3 PosB;
        private float _padding1;
    }

    public RelationLineParams(float lineWidth, Vector3 camera)
    {
        GeoParams = ShaderParams.FromUniformBlockDataAndSamplers(
            "ubGeometry",
            new GeometryData() { LineWidth = lineWidth, Camera = camera },
            "ubGeometryBuffer",
            [],
            out _geobuffer
        );
        VertParams = ShaderParams.FromUniformBlockDataAndSamplers(
            "ubPos",
            new VertexData() { },
            "ubPosBuffer",
            [],
            out _vertbuffer
        );
    }
    public float LineWidth
    {
        get => _geobuffer.Data.LineWidth;
        set => _geobuffer.SetData(_geobuffer.Data with { LineWidth = value });
    }
    public Vector3 Camera
    {
        get => _geobuffer.Data.Camera;
        set => _geobuffer.SetData(_geobuffer.Data with { Camera = value });
    }
    
    public Vector3 PosA
    {
        get => _vertbuffer.Data.PosA;
        set => _vertbuffer.SetData(_vertbuffer.Data with { PosA = value });
    }
    public Vector3 PosB
    {
        get => _vertbuffer.Data.PosB;
        set => _vertbuffer.SetData(_vertbuffer.Data with { PosB = value });
    }
}
