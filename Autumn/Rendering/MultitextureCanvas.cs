using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Storage;
using SceneGL;
using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using SceneGL.Materials;
using SceneGL.Materials.Common;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

/// <summary>
/// Canvas to render onto from other passes, mainly color and extras
/// </summary>
internal static class Canvas
{
	public static ShaderSource s_vertexShader =>
		new(
			"Canvas.vert",
			ShaderType.VertexShader,
            """
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aTexCoords;

            out vec2 TexCoords;

            void main()
            {
                gl_Position = vec4(aPos.x, aPos.y, 0.0, 1.0);
                TexCoords = aTexCoords;
            }  
            """
		);
	public static ShaderSource s_fragmentShader =>
		new(
			"Canvas.frag",
			ShaderType.FragmentShader,
            """
            #version 330 core

            in vec2 TexCoords;

            uniform sampler2D FgTexture;
            uniform sampler2D BgTexture;
            uniform sampler2DShadow Depth;

            uniform vec3 uFogColor;
            uniform vec3 uNearFarDensity;


            uniform uint uBools;
            // first bit -> outline enabled
            // second-third bit -> fog type
            //  0 -> disabled, 1 -> linear, 2 -> exponential, 3 -> exp_sqr

            out vec4 FragColor;

            const float offset = 1.0 / 350.0; 

            float ToLinear(float depth)
            {
                float z = depth * 10 - 9.5; // Back to NDC 
                z = clamp(z, -1, 10);
                return (2.0 * uNearFarDensity.x * uNearFarDensity.y) / (uNearFarDensity.y + uNearFarDensity.x - z * (uNearFarDensity.y - uNearFarDensity.x));
            }

            // float ToExp(float depth)
            // {
            //     float z = depth * 10.0 - 10.0; // Back to NDC 
            //     return exp(-uNearFarDensity.z*z);
            // }

            float ToExp(float depth)
            {
                float z = depth * 10 - 1;
                return exp(-(z/ uNearFarDensity.z));
            }
            float ToExpSqr(float depth)
            {
                float z = depth * 10 - 1;
                return exp(-(pow(z, 2)/ uNearFarDensity.z));
            }

            void main()
            {
                FragColor = texture(BgTexture, TexCoords);
                if ((uBools & 1u) == 1u)
                {
                    vec2 offsets[9] = vec2[](
                        vec2(-offset,  offset), // top-left
                        vec2( 0.0f,    offset), // top-center
                        vec2( offset,  offset), // top-right
                        vec2(-offset,  0.0f),   // center-left
                        vec2( 0.0f,    0.0f),   // center-center
                        vec2( offset,  0.0f),   // center-right
                        vec2(-offset, -offset), // bottom-left
                        vec2( 0.0f,   -offset), // bottom-center
                        vec2( offset, -offset)  // bottom-right    
                    );

                    float kernel[9] = float[](
                        2, 2, 2,
                        2, -22, 2,
                        2, 2, 2
                    );
                    float hasSelection = 0;
                    vec3 sampleTex[9];
                    for(int i = 0; i < 9; i++)
                    {
                        sampleTex[i] = vec3(texture(FgTexture, TexCoords.st + offsets[i]).r);
                        hasSelection += (texture(FgTexture, TexCoords.st + offsets[i]).r > 0.3 ? 1 : 0);
                    }
                    if (hasSelection > 0.5)
                    {
                        vec3 col = vec3(0.0);
                        for(int i = 0; i < 9; i++)
                            col += sampleTex[i] * kernel[i];
                        
                        FragColor.rgb += clamp(col, vec3(0), vec3(1));
                    }
                }
                // Linear fog
                if (((uBools >> 1u) & 3u) == 1u)
                {
                    FragColor.rgb = mix(FragColor.rgb, uFogColor, clamp(1 - log(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))) * log(1/ uNearFarDensity.y) * log(uNearFarDensity.x), 0, 1));
                    // FragColor.rgb += uFogColor * vec3(ToLinear(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))));
                }
                // Exponential fog
                else if (((uBools >> 1u) & 3u) == 2u)
                {
                   FragColor.rgb = mix(FragColor.rgb, uFogColor, clamp(ToExp(log(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))) * log(1/ 1000000.0)), 0.0, 1.0)); 
                }
                // Exponential Squared fog
                else if (((uBools >> 1u) & 3u) == 3u)
                {
                   FragColor.rgb = mix(FragColor.rgb, uFogColor, clamp(ToExpSqr(log(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))) * log(1/ 1/ 1000000.0)), 0.0, 1.0)); 
                }

                vec4 tx2 = texture(FgTexture, TexCoords);
                if (tx2.g * 2 < tx2.b)
                    FragColor *= 0.6;
                // FragColor.rgb = vec3(ToExpSqr(log(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))) * log(1 / 1000000.0)));
                // FragColor.rgb = vec3(1 - log(texture(Depth, vec3(TexCoords.x, TexCoords.y, 0.01))) * log(1/ uNearFarDensity.x / 1000));
            }
            """
		);
    public static ShaderProgram Program { get; } = new(s_vertexShader, s_fragmentShader);
    public static bool TryUse(
        GL gl,
        out ProgramUniformScope scope
    ) => Program.TryUse(gl, null, [_samplers], out scope, out _);


    private static uint _smpl = 0; 
    public static uint CreateTextureSamplerA(GL gl)
    {
        if (_smpl == 0)
        { 
        TextureMagFilter magFilter = TextureMagFilter.Nearest;

        TextureMinFilter minFilter = TextureMinFilter.Nearest;

        _smpl = SamplerHelper.CreateSampler2D(gl, TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge, magFilter, minFilter);
        }
        return _smpl;
    }
    static List<SamplerBinding> samplerBindings = new();        
    private static ShaderParams _samplers;


    internal static class CanvasRenderer
    {
        private struct Vertex
        {
            [VertexAttribute(CombinerMaterial.POSITION_LOC, 2, VertexAttribPointerType.Float, false)]
            public Vector2 Position;
            [VertexAttribute(CombinerMaterial.COLOR_LOC, 2, VertexAttribPointerType.Float, false)]
            public Vector2 UV;
        }

        private static RenderableModel? s_model;

        public static void Initialize(GL gl)
        {
            s_model = GenerateCanvas(gl);
            CreateTextureSamplerA(gl);
            samplerBindings.Add(new ("STexture", 0, 0));
            samplerBindings.Add(new ("STexture2", 0, 0));
            samplerBindings.Add(new ("STexture3", 0, 0));
        }
        public static RenderableModel GenerateCanvas(GL gl)
        {
            ModelBuilder<ushort, Vertex> builder = new();
            builder!.AddPlane(
                new Vertex { Position = new Vector2(1,   1)  , UV = new Vector2(1, 1) },
                new Vertex { Position = new Vector2(1,  -1)  , UV = new Vector2(1, 0) },
                new Vertex { Position = new Vector2(-1,  1)  , UV = new Vector2(0, 1) },
                new Vertex { Position = new Vector2(-1, -1)  , UV = new Vector2(0, 0) }
            );

            return builder.GetModel(gl);
        }
        public static bool HasToReset = true;
        public static void Reset(uint tx1, uint tx2, uint tx3)
        {
            samplerBindings[0] = new ("BgTexture", _smpl, tx1);
            samplerBindings[1] = new ("FgTexture", _smpl, tx2);
            samplerBindings[2] = new ("Depth", _smpl, tx3);
            HasToReset = false;
        }


        public static void Render(GL gl, MainWindowContext window)
        {
            if (HasToReset) 
            {
                Reset(window.SceneFramebuffer.GetColorTexture(0), window.SceneFramebuffer.GetColorTexture(2), window.SceneFramebuffer.DepthStencilTexture);
                _samplers = ShaderParams.FromSamplers(samplerBindings.ToArray());
            }
            if (!Canvas.TryUse(gl, out ProgramUniformScope scope))
                return;

            using (scope)
            {
                gl.Disable(EnableCap.CullFace);

                uint uBools = window.ContextHandler.SystemSettings.EXPERIMENTAL_SelectionOutline ? 1u : 0u;
                uBools += (uint)window.GetFogType() << 1;
                
                if (Program.TryGetUniformLoc("uBools", out int location))
                    gl.Uniform1(location, uBools);

                var fog = window.GetSelectedFog();
                if (fog != null)
                {    
                    if (Program.TryGetUniformLoc("uFogColor", out location))
                        gl.Uniform3(location, fog!.Color);
                    if (Program.TryGetUniformLoc("uNearFarDensity", out location))
                    {
                        if (fog.MinDepth < fog.MaxDepth)
                            gl.Uniform3(location, new Vector3(fog.MinDepth, fog.MaxDepth, fog.Density));
                        else
                            gl.Uniform3(location, new Vector3(0, 0, fog.Density));

                    }
                }
                s_model!.Draw(gl);
                gl.Enable(EnableCap.CullFace);
            }
        }

    }
}