using System.Diagnostics;
using System.Numerics;
using SceneGL;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/InfiniteGrid.cs

namespace Autumn.Rendering
{
    internal static class InfiniteGrid
    {
        private struct Vertex
        {
            [VertexAttribute(AttributeShaderLoc.Loc0, 3, VertexAttribPointerType.Float, false)]
            public Vector3 Position;
        }

        private static bool s_initialized = false;

        private static ShaderProgram? s_shaderProgram;
        private static RenderableModel s_model;

        public static readonly ShaderSource VertexSource =
            new(
                "InfiniteGrid.vert",
                ShaderType.VertexShader,
                """
                #version 330

                uniform mat4x3 uTransform;
                uniform mat4x4 uViewProjection;
                layout (location = 0) in vec3 aPosition;

                out vec2 vTexCoord;
                out vec4 vColor;

                void main() {

                    vec3 pos = uTransform*vec4(aPosition, 1.0);

                    vTexCoord = pos.xz;

                    gl_Position = uViewProjection*vec4(pos, 1.0);
                }
                """
            );

        public static readonly ShaderSource FragmentSource =
            new(
                "InfiniteGrid.frag",
                ShaderType.FragmentShader,
                """
                #version 330

                uniform vec4 uColor;
                uniform sampler2D uTex;

                in vec2 vTexCoord;

                out vec4 oColor;

                void main() {
                    
                    oColor = vec4(0.5);
                    

                    float fwx = fwidth(vTexCoord.x);
                    float fwy = fwidth(vTexCoord.y);

                    float _log = max(-0.2,log(max(fwx,fwy)*30)/log(10.0));

                    float a = 0.0;
                    float cellSize = pow(10.0,floor(_log));

                    vec2 cellCoords = mod(vTexCoord,cellSize);

                    

                    a = (1-fract(_log)) * 
                    max(
                        smoothstep( 
                        (cellSize/2.0-fwx*1.1),
                        (cellSize/2.0),
                        abs(cellCoords.x-cellSize/2.0)),
                        smoothstep( 
                        (cellSize/2.0-fwy*1.1),
                        (cellSize/2.0),
                        abs(cellCoords.y-cellSize/2.0))
                    );

                    cellSize = pow(10.0,ceil(_log));

                    cellCoords = mod(vTexCoord,cellSize);

                    

                    a += fract(_log) * 
                    max(
                        smoothstep( 
                        (cellSize/2.0-fwx*1.1),
                        (cellSize/2.0),
                        abs(cellCoords.x-cellSize/2.0)),
                        smoothstep( 
                        (cellSize/2.0-fwy*1.1),
                        (cellSize/2.0),
                        abs(cellCoords.y-cellSize/2.0))
                    );
                    
                    oColor.a *= a;

                    a = 2.0*smoothstep(fwy*2.0,0,abs(vTexCoord.y));
                    oColor.rbg = mix(oColor.rbg,vec3(1.0,0.0,0.0),a);
                    oColor.a += a;

                    a = 2.0*smoothstep(fwx*2.0,0,abs(vTexCoord.x));
                    oColor.rbg = mix(oColor.rbg,vec3(0.0,0.0,1.0),a);
                    oColor.a += a;

                    oColor.a*=gl_FragCoord.a*10.0;

                    if(oColor.a==0.0)
                       discard;
                }
                """
            );

        public static void Initialize(GL gl)
        {
            if (s_initialized)
                return;

            s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

            s_initialized = true;

            s_model = new ModelBuilder<ushort, Vertex>()
                .AddPlane(
                    new Vertex { Position = new Vector3(-1000, 0, -1000) },
                    new Vertex { Position = new Vector3(+1000, 0, -1000) },
                    new Vertex { Position = new Vector3(-1000, 0, +1000) },
                    new Vertex { Position = new Vector3(+1000, 0, +1000) }
                )
                .GetModel(gl);
        }

        private static Vector4 _zero = Vector4.Zero;
        private static Matrix4x4 _identity = Matrix4x4.Identity;

        public static void Render(GL gl, in Matrix4x4 viewProjection) =>
            Render(gl, ref _zero, in _identity, viewProjection);

        public static void Render(
            GL gl,
            ref Vector4 color,
            in Matrix4x4 transform,
            in Matrix4x4 viewProjection
        )
        {
            if (!s_initialized)
                throw new InvalidOperationException(
                    $@"{nameof(InfiniteGrid)} must be initialized before any calls to {nameof(Render)}"
                );

            if (s_shaderProgram!.TryUse(gl, out _))
            {
                if (s_shaderProgram.TryGetUniformLoc("uColor", out int loc))
                    gl.Uniform4(loc, ref color);

                if (s_shaderProgram.TryGetUniformLoc("uTransform", out loc))
                {
                    Span<float> floats = stackalloc float[3 * 4];

                    const int N = 3;

                    {
                        var mtx = transform;

                        floats[(1 - 1) * N + (1 - 1)] = mtx.M11;
                        floats[(1 - 1) * N + (2 - 1)] = mtx.M12;
                        floats[(1 - 1) * N + (3 - 1)] = mtx.M13;

                        floats[(2 - 1) * N + (1 - 1)] = mtx.M21;
                        floats[(2 - 1) * N + (2 - 1)] = mtx.M22;
                        floats[(2 - 1) * N + (3 - 1)] = mtx.M23;

                        floats[(3 - 1) * N + (1 - 1)] = mtx.M31;
                        floats[(3 - 1) * N + (2 - 1)] = mtx.M32;
                        floats[(3 - 1) * N + (3 - 1)] = mtx.M33;

                        floats[(4 - 1) * N + (1 - 1)] = mtx.M41;
                        floats[(4 - 1) * N + (2 - 1)] = mtx.M42;
                        floats[(4 - 1) * N + (3 - 1)] = mtx.M43;
                    }

                    gl.UniformMatrix4x3(loc, 1, false, in floats[0]);
                }

                if (s_shaderProgram.TryGetUniformLoc("uViewProjection", out loc))
                {
                    Span<float> floats = stackalloc float[4 * 4];

                    const int N = 4;

                    {
                        var mtx = viewProjection;

                        floats[(1 - 1) * N + (1 - 1)] = mtx.M11;
                        floats[(1 - 1) * N + (2 - 1)] = mtx.M12;
                        floats[(1 - 1) * N + (3 - 1)] = mtx.M13;
                        floats[(1 - 1) * N + (4 - 1)] = mtx.M14;

                        floats[(2 - 1) * N + (1 - 1)] = mtx.M21;
                        floats[(2 - 1) * N + (2 - 1)] = mtx.M22;
                        floats[(2 - 1) * N + (3 - 1)] = mtx.M23;
                        floats[(2 - 1) * N + (4 - 1)] = mtx.M24;

                        floats[(3 - 1) * N + (1 - 1)] = mtx.M31;
                        floats[(3 - 1) * N + (2 - 1)] = mtx.M32;
                        floats[(3 - 1) * N + (3 - 1)] = mtx.M33;
                        floats[(3 - 1) * N + (4 - 1)] = mtx.M34;

                        floats[(4 - 1) * N + (1 - 1)] = mtx.M41;
                        floats[(4 - 1) * N + (2 - 1)] = mtx.M42;
                        floats[(4 - 1) * N + (3 - 1)] = mtx.M43;
                        floats[(4 - 1) * N + (4 - 1)] = mtx.M44;
                    }

                    gl.UniformMatrix4(loc, 1, false, in floats[0]);
                }

                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                gl.ColorMask(1, false, false, false, false);

                gl.Disable(EnableCap.CullFace);

                s_model.Draw(gl);

                gl.Disable(EnableCap.Blend);
                gl.UseProgram(0);

                gl.Enable(EnableCap.CullFace);
                gl.ColorMask(1, true, true, true, true);
            }
            else
                Debugger.Break();
        }

        public static void CleanUp(GL gl) => s_model?.CleanUp(gl);
    }
}
