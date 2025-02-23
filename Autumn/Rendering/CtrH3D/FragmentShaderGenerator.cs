using System.Globalization;
using System.Text;
using Autumn.Enums;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Math3D;
using SPICA.PICA.Commands;
using SPICA.PICA.Shader;

// Original file from: https://github.com/KillzXGaming/SPICA/blob/master/SPICA.Rendering/Shaders/FragmentShaderGenerator.cs

namespace Autumn.Rendering.CtrH3D;

internal class FragmentShaderGenerator
{
    public const string EmissionUniform = "u_EmissionColor";
    public const string AmbientUniform = "u_AmbientColor";
    public const string DiffuseUniform = "u_DiffuseColor";
    public const string Specular0Uniform = "u_Specular0Color";
    public const string Specular1Uniform = "u_Specular1Color";
    public const string Constant0Uniform = "u_Constant0Color";
    public const string Constant1Uniform = "u_Constant1Color";
    public const string Constant2Uniform = "u_Constant2Color";
    public const string Constant3Uniform = "u_Constant3Color";
    public const string Constant4Uniform = "u_Constant4Color";
    public const string Constant5Uniform = "u_Constant5Color";
    public const string Constant5Variable = "v_Constant5Color";
    public const string AlphaRefUniform = "u_AlphaReference";
    public const string SelectionUniform = "u_SelectionColor";
    public const string DebugModeUniform = "u_DebugMode";
    public const string DebugLUTModeUniform = "u_DebugLUTMode";
    public const string CombBufferUniform = "u_CombBufferColor";

    private StringBuilder SB;

    private H3DMaterialParams Params;

    private bool[] HasTexColor;

    public FragmentShaderGenerator(H3DMaterialParams Params)
    {
        this.Params = Params;
    }

    public string GetFragShader()
    {
        string fragmentShaderBase = """
            #version 150
            #extension GL_ARB_gpu_shader5 : require

            struct Light_t {
                vec3 Position;
                int DisableConst5;
                vec3 Direction;
                vec4 Ambient;
                vec4 Diffuse;
                vec4 Specular0;
                vec4 Specular1;
                float AttScale;
                float AttBias;
                float AngleLUTScale;
                int AngleLUTInput;
                int SpotAttEnb;
                int DistAttEnb;
                int TwoSidedDiff;
                int Directional;
                vec4 ConstantColor5;
            };

            uniform sampler2D Textures[3];
            uniform sampler2D LUTs[6];
            uniform sampler2D LightDistanceLUT[3];
            uniform sampler2D LightAngleLUT[3];
            uniform sampler2D UVTestPattern;

            uniform samplerCube TextureCube;

            vec3 QuatRotate(vec4 q, vec3 v) {
                return v + 2 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
            }

            float SampleLUT(int lidx, float idx, float s) {
                float x = (idx + 1) * 0.5;
                float r = texture(LUTs[lidx], vec2(x, 0)).r;

                return min(r * s, 1);
            }

            layout(std140) uniform ubMaterial {
                int LightsCount;
                Light_t Lights[3];
                vec4 SAmbient;
            """;

        SB = new StringBuilder(fragmentShaderBase);

        HasTexColor = new bool[] { false, false, false };

        int Index = 0;

        bool HasFragColors = false;

        SB.AppendLine();
        SB.AppendLine($"    vec4 {EmissionUniform};");
        SB.AppendLine($"    vec4 {AmbientUniform};");
        SB.AppendLine($"    vec4 {DiffuseUniform};");
        SB.AppendLine($"    vec4 {Specular0Uniform};");
        SB.AppendLine($"    vec4 {Specular1Uniform};");
        SB.AppendLine($"    vec4 {Constant0Uniform};");
        SB.AppendLine($"    vec4 {Constant1Uniform};");
        SB.AppendLine($"    vec4 {Constant2Uniform};");
        SB.AppendLine($"    vec4 {Constant3Uniform};");
        SB.AppendLine($"    vec4 {Constant4Uniform};");
        SB.AppendLine($"    vec4 {Constant5Uniform};");
        SB.AppendLine($"    vec4 {CombBufferUniform};");
        SB.AppendLine($"    float {AlphaRefUniform};");
        SB.AppendLine($"    vec4 {SelectionUniform};");
        SB.AppendLine($"    vec3 CameraView;");
        SB.AppendLine("};");
        SB.AppendLine();
        SB.AppendLine($"uniform int {DebugModeUniform};");
        SB.AppendLine($"uniform int {DebugLUTModeUniform};");
        SB.AppendLine();
        SB.AppendLine("uniform uint uPickingId;");

        SB.AppendLine();
        SB.AppendLine($"in vec4 {ShaderOutputRegName.QuatNormal};");
        SB.AppendLine($"in vec4 {ShaderOutputRegName.Color};");
        SB.AppendLine($"in vec4 {ShaderOutputRegName.TexCoord0};");
        SB.AppendLine($"in vec4 {ShaderOutputRegName.TexCoord1};");
        SB.AppendLine($"in vec4 {ShaderOutputRegName.TexCoord2};");
        SB.AppendLine($"in vec4 {ShaderOutputRegName.View};");
        SB.AppendLine();
        SB.AppendLine("out vec4 Output;");
        SB.AppendLine("out uint oPickingId;");
        SB.AppendLine();
        SB.AppendLine("//SPICA auto-generated Fragment Shader");
        SB.AppendLine();
        SB.AppendLine("void main() {");
        SB.AppendLine();
        SB.AppendLine("    oPickingId = uPickingId;");

        SB.AppendLine("    vec4 Previous;");
        SB.AppendLine("    vec4 DebugDist0;");
        SB.AppendLine("    vec4 DebugDist1;");
        SB.AppendLine("    vec4 DebugFresnel;");
        SB.AppendLine("    vec4 DebugSpec;");
        SB.AppendLine($"    vec4 CombBuffer = {CombBufferUniform};");
        SB.AppendLine($"    vec4 FragPriColor = vec4(0, 0, 0, 1);");
        SB.AppendLine("    vec4 FragSecColor = vec4(0, 0, 0, 1);");
        SB.AppendLine($"    vec4 {Constant5Variable} = {Constant5Uniform};");

        int stageId = 0;
        foreach (PICATexEnvStage Stage in Params.TexEnvStages)
        {
            bool HasColor = !Stage.IsColorPassThrough;
            bool HasAlpha = !Stage.IsAlphaPassThrough;

            //Debug preview. Display only the target stage
            if (
                H3DMaterialParams.DisplayStageID != -1
                && stageId > H3DMaterialParams.DisplayStageID
            )
            {
                HasColor = false;
                HasAlpha = false;
            }

            string[] ColorArgs = new string[3];
            string[] AlphaArgs = new string[3];

            string Constant = Stage.Constant switch
            {
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Constant1 => Constant1Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Constant2 => Constant2Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Constant3 => Constant3Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Constant4 => Constant4Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Constant5 => Constant5Variable,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Diffuse => DiffuseUniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Ambient => AmbientUniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Specular0 => Specular0Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Specular1 => Specular1Uniform,
                SPICA.Formats.CtrGfx.Model.Material.GfxTexEnvConstant.Emission => EmissionUniform,
                _ => Constant0Uniform,
            };

            for (int Param = 0; Param < 3; Param++)
            {
                //Check if the Fragment lighting colors are used
                if (
                    !HasFragColors
                    && (HasColor || HasAlpha)
                    && Params.Flags.HasFlag(H3DMaterialFlags.IsFragmentLightingEnabled)
                )
                {
                    GenFragColors();
                    HasFragColors = true;
                }

                //Check if any of the texture units are used
                for (int Unit = 0; Unit < 3; Unit++)
                {
                    if (
                        !HasTexColor[Unit]
                        && (
                            Stage.Source.Color[Param] == PICATextureCombinerSource.Texture0 + Unit
                            || Stage.Source.Alpha[Param]
                                == PICATextureCombinerSource.Texture0 + Unit
                        )
                    )
                    {
                        GenTexColor(Unit);
                        HasTexColor[Unit] = true;
                    }
                }

                string ColorArg = GetCombinerSource(Stage.Source.Color[Param], Constant);
                string AlphaArg = GetCombinerSource(Stage.Source.Alpha[Param], Constant);

                switch ((PICATextureCombinerColorOp)((int)Stage.Operand.Color[Param] & ~1))
                {
                    case PICATextureCombinerColorOp.Alpha:
                        ColorArg = $"{ColorArg}.aaaa";
                        break;
                    case PICATextureCombinerColorOp.Red:
                        ColorArg = $"{ColorArg}.rrrr";
                        break;
                    case PICATextureCombinerColorOp.Green:
                        ColorArg = $"{ColorArg}.gggg";
                        break;
                    case PICATextureCombinerColorOp.Blue:
                        ColorArg = $"{ColorArg}.bbbb";
                        break;
                }

                switch ((PICATextureCombinerAlphaOp)((int)Stage.Operand.Alpha[Param] & ~1))
                {
                    case PICATextureCombinerAlphaOp.Alpha:
                        AlphaArg = $"{AlphaArg}.a";
                        break;
                    case PICATextureCombinerAlphaOp.Red:
                        AlphaArg = $"{AlphaArg}.r";
                        break;
                    case PICATextureCombinerAlphaOp.Green:
                        AlphaArg = $"{AlphaArg}.g";
                        break;
                    case PICATextureCombinerAlphaOp.Blue:
                        AlphaArg = $"{AlphaArg}.b";
                        break;
                }

                if (((int)Stage.Operand.Color[Param] & 1) != 0)
                    ColorArg = $"1 - {ColorArg}";

                if (((int)Stage.Operand.Alpha[Param] & 1) != 0)
                    AlphaArg = $"1 - {AlphaArg}";

                if (
                    H3DMaterialParams.DisplayStageID == stageId
                    && H3DMaterialParams.DisplayStageDirect
                )
                {
                    //Blank out the tev stage if previous pass
                    if (ColorArg == "Previous")
                    {
                        //Default to black or white to empty out the effect
                        ColorArg = "vec4(0, 0, 0, 0)";
                        if (
                            Stage.Combiner.Color == PICATextureCombinerMode.Modulate
                            || Stage.Combiner.Color == PICATextureCombinerMode.MultAdd && Param == 0
                            || Stage.Combiner.Color == PICATextureCombinerMode.AddMult && Param == 2
                        )
                        {
                            ColorArg = "vec4(1, 1, 1, 1)";
                        }
                    }
                }

                ColorArgs[Param] = ColorArg;
                AlphaArgs[Param] = AlphaArg;
            }

            if (HasColor)
                GenCombinerColor(Stage, stageId, ColorArgs);

            if (HasAlpha)
                GenCombinerAlpha(Stage, AlphaArgs);

            int ColorScale = 1 << (int)Stage.Scale.Color;
            int AlphaScale = 1 << (int)Stage.Scale.Alpha;

            if (ColorScale != 1)
                SB.AppendLine($"\tOutput.rgb = min(Output.rgb * {ColorScale}, 1);");

            if (AlphaScale != 1)
                SB.AppendLine($"\tOutput.a = min(Output.a * {AlphaScale}, 1);");

            if (Stage.UpdateColorBuffer)
                SB.AppendLine("\tCombBuffer.rgb = Previous.rgb;");

            if (Stage.UpdateAlphaBuffer)
                SB.AppendLine("\tCombBuffer.a = Previous.a;");

            if (Index < 6 && (HasColor || HasAlpha))
                SB.AppendLine("\tPrevious = Output;");

            stageId++;
        }

        if (Params.AlphaTest.Enabled)
        {
            //Note: This is the condition to pass the test, so we actually test the inverse to discard
            switch (Params.AlphaTest.Function)
            {
                case PICATestFunc.Never:
                    SB.AppendLine("\tdiscard;");
                    break;
                case PICATestFunc.Equal:
                    SB.AppendLine($"\tif (Output.a != {AlphaRefUniform}) discard;");
                    break;
                case PICATestFunc.Notequal:
                    SB.AppendLine($"\tif (Output.a == {AlphaRefUniform}) discard;");
                    break;
                case PICATestFunc.Less:
                    SB.AppendLine($"\tif (Output.a >= {AlphaRefUniform}) discard;");
                    break;
                case PICATestFunc.Lequal:
                    SB.AppendLine($"\tif (Output.a > {AlphaRefUniform}) discard;");
                    break;
                case PICATestFunc.Greater:
                    SB.AppendLine($"\tif (Output.a <= {AlphaRefUniform}) discard;");
                    break;
                case PICATestFunc.Gequal:
                    SB.AppendLine($"\tif (Output.a < {AlphaRefUniform}) discard;");
                    break;
            }
        }

        SB.AppendLine($"\tif ({DebugModeUniform} == 1)"); //Show normals
        if (HasFragColors)
            SB.AppendLine($"\t    Output.rgb = (Normal.rgb * 0.5) + 0.5;");
        else
            SB.AppendLine($"\t    Output.rgb = vec3(0);");

        SB.AppendLine($"\telse if ({DebugModeUniform} == 2)"); //Show lighting
        if (HasFragColors)
            SB.AppendLine($"\t    Output.rgb = FragPriColor.rgb;");
        else
            SB.AppendLine($"\t    Output.rgb = vec3(1);");

        SB.AppendLine($"\telse if ({DebugModeUniform} == 3)"); //Show texture 0
        SB.AppendLine($"\t    Output.rgb = {GetTextureUniform(0)}.rgb;");

        SB.AppendLine($"\telse if ({DebugModeUniform} == 4)"); //Show vertex color
        SB.AppendLine($"\t    Output.rgb = {ShaderOutputRegName.Color}.rgb;");

        SB.AppendLine($"\telse if ({DebugModeUniform} == 5)"); //Show tex coords
        SB.AppendLine(
            $"\t    Output.rgb = vec3({ShaderOutputRegName.TexCoord0}.x, {ShaderOutputRegName.TexCoord0}.y, 1.0).rgb;"
        );

        SB.AppendLine($"\telse if ({DebugModeUniform} == 6)"); //Show uv pattern
        SB.AppendLine(
            $"\t    Output.rgb = texture(UVTestPattern, {ShaderOutputRegName.TexCoord0}.xy).rgb;"
        );

        // SB.AppendLine($"\telse if ({DebugModeUniform} == 7)"); //Show weights
        // SB.AppendLine($"\t    Output.rgb = WeightPreview.rgb;"); //todo WeightPreview requires switching to forced default vertex shader

        SB.AppendLine($"\telse if ({DebugModeUniform} == 8)"); //Show tangent
        if (HasFragColors)
            SB.AppendLine($"\t    Output.rgb = Tangent.rgb;");
        else
            SB.AppendLine($"\t    Output.rgb = vec3(0);");

        SB.AppendLine($"\telse if ({DebugModeUniform} == 10)"); //Show specular
        if (HasFragColors)
            SB.AppendLine($"\t    Output.rgb = FragSecColor.rgb;");
        else
            SB.AppendLine($"\t    Output.rgb = vec3(0);");

        SB.AppendLine($"\telse if ({DebugModeUniform} != 0)"); //Empty
        SB.AppendLine($"\t    Output.rgb = vec3(0);");

        if (HasFragColors)
        {
            SB.AppendLine($"\tif ({DebugLUTModeUniform} == 1)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugDist0.rgb;");

            SB.AppendLine($"\telse if ({DebugLUTModeUniform} == 2)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugDist1.rgb;");

            SB.AppendLine($"\telse if ({DebugLUTModeUniform} == 3)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugFresnel.rgb;");

            SB.AppendLine($"\telse if ({DebugLUTModeUniform} == 4)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugSpec.rrr;");

            SB.AppendLine($"\telse if ({DebugLUTModeUniform} == 5)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugSpec.ggg;");

            SB.AppendLine($"\telse if ({DebugLUTModeUniform} == 6)"); //LUT
            SB.AppendLine($"\t    Output.rgb = DebugSpec.bbb;");
        }

        SB.AppendLine($"Output.rgb += {SelectionUniform}.rgb * {SelectionUniform}.a;");

        if (Params.RenderLayer == (int)H3DMeshLayer.Opaque)
        {
            SB.AppendLine("Output.a = 1;");
        }
        
        SB.AppendLine("}");

        return SB.ToString();
    }

    private void GenFragColors()
    {
        //See Model and Mesh class for the LUT mappings
        string Dist0 = GetLUTInput(Params.LUTInputSelection.Dist0, Params.LUTInputScale.Dist0, 0);
        string Dist1 = GetLUTInput(Params.LUTInputSelection.Dist1, Params.LUTInputScale.Dist1, 1);
        string Fresnel = GetLUTInput(
            Params.LUTInputSelection.Fresnel,
            Params.LUTInputScale.Fresnel,
            2
        );
        string ReflecR = GetLUTInput(
            Params.LUTInputSelection.ReflecR,
            Params.LUTInputScale.ReflecR,
            3
        );
        string ReflecG = GetLUTInput(
            Params.LUTInputSelection.ReflecG,
            Params.LUTInputScale.ReflecG,
            4
        );
        string ReflecB = GetLUTInput(
            Params.LUTInputSelection.ReflecB,
            Params.LUTInputScale.ReflecB,
            5
        );

        string Color = $"{EmissionUniform} + {AmbientUniform} * SAmbient";

        SB.AppendLine("\t //Fragment lighting Enabled");
        SB.AppendLine($"\tFragPriColor = vec4(({Color}).rgb, 1);");
        SB.AppendLine("\tFragSecColor = vec4(0, 0, 0, 1);");

        if (Params.BumpMode == H3DBumpMode.AsBump || Params.BumpMode == H3DBumpMode.AsTangent)
        {
            if (!HasTexColor[Params.BumpTexture])
            {
                GenTexColor(Params.BumpTexture);
                HasTexColor[Params.BumpTexture] = true;
            }
        }

        string BumpColor = $"Color{Params.BumpTexture}.xyz * 2 - 1";

        switch (Params.BumpMode)
        {
            case H3DBumpMode.AsBump:
                SB.AppendLine($"\tvec3 SurfNormal = {BumpColor};");
                SB.AppendLine("\tvec3 SurfTangent = vec3(1, 0, 0);");
                break;

            case H3DBumpMode.AsTangent:
                SB.AppendLine("\tvec3 SurfNormal = vec3(0, 0, 1);");
                SB.AppendLine($"\tvec3 SurfTangent = {BumpColor};");
                break;

            default: /* NotUsed */
                SB.AppendLine("\tvec3 SurfNormal = vec3(0, 0, 1);");
                SB.AppendLine("\tvec3 SurfTangent = vec3(1, 0, 0);");
                break;
        }

        //Recalculates the Z axis on the normal to give more precision.
        //For Tangent it was reported that the 3DS doesn't recalculate Z
        //(or maybe it just doesn't matter for the final formula where it is used).
        if ((Params.FragmentFlags & H3DFragmentFlags.IsBumpRenormalizeEnabled) != 0)
        {
            SB.AppendLine("\tSurfNormal.z = sqrt(max(1 - dot(SurfNormal.xy, SurfNormal.xy), 0));");
        }

        string HalfProj = "Half - Normal / dot(Normal, Normal) * dot(Normal, Half)";

        string QuatNormal = $"{ShaderOutputRegName.QuatNormal}";
        string View = $"{ShaderOutputRegName.View}";

        SB.AppendLine($"\tvec4 NormQuat = normalize({QuatNormal});");
        SB.AppendLine($"\tvec3 Normal = QuatRotate(NormQuat, SurfNormal);");
        SB.AppendLine($"\tvec3 Tangent = QuatRotate(NormQuat, SurfTangent);");

        //Lights loop start
        SB.AppendLine();
        SB.AppendLine("\tfor (int i = 0; i < LightsCount; i++) {");

        // Prevents CameraView from being the exact same as any of the axis, thus making it impossible for flickering due to bad angles to happen.
        SB.AppendLine($"\t\tfloat Roundedx = (abs(CameraView.x) == 1 || CameraView.x == 0) ? CameraView.x+0.001 : CameraView.x;");
        SB.AppendLine($"\t\tfloat Roundedy = (abs(CameraView.y) == 1 || CameraView.y == 0) ? CameraView.y+0.001 : CameraView.y;");
        SB.AppendLine($"\t\tfloat Roundedz = (abs(CameraView.z) == 1 || CameraView.z == 0) ? CameraView.z+0.001 : CameraView.z;");
        SB.AppendLine($"\t\tvec3 Rounded = vec3(Roundedx, Roundedy, Roundedz);");

        // Same but for Lights[i].Position.
        SB.AppendLine($"\t\tvec3 RPos = Lights[i].Position;");
        SB.AppendLine("\t\tif (Lights[i].Directional != 0) {");

        SB.AppendLine($"\t\t\tRPos.x = (abs(RPos.x) == 1 || RPos.x == 0) ? RPos.x+0.001 : RPos.x;");
        SB.AppendLine($"\t\t\tRPos.y = (abs(RPos.y) == 1 || RPos.y == 0) ? RPos.y+0.001 : RPos.y;");
        SB.AppendLine($"\t\t\tRPos.z = (abs(RPos.z) == 1 || RPos.z == 0) ? RPos.z+0.001 : RPos.z;");
        SB.AppendLine("\t\t}");
        SB.AppendLine(
            "\t\tvec3 Light = (Lights[i].Directional != 0)"
                + " ? normalize(RPos)"
                + $" : normalize(RPos + Rounded);"
        );

        SB.AppendLine($"\t\tvec3 Half = normalize(normalize(Rounded) + Light);");
        SB.AppendLine("\t\tfloat CosNormalHalf = dot(Normal, Half);");
        SB.AppendLine($"\t\tfloat CosViewHalf = dot(normalize(Rounded), Half);");
        SB.AppendLine($"\t\tfloat CosNormalView = dot(Normal, normalize(Rounded));");
        SB.AppendLine("\t\tfloat CosLightNormal = dot(Light, Normal);");
        SB.AppendLine("\t\tfloat CosLightSpot = dot(Light, Lights[i].Direction);");
        SB.AppendLine($"\t\tfloat CosPhi = dot({HalfProj}, Tangent);");

        SB.AppendLine(
            "\t\tfloat ln = (Lights[i].TwoSidedDiff != 0)"
                + " ? abs(CosLightNormal)"
                + " : max(CosLightNormal, 0);"
        );

        SB.AppendLine("\t\tfloat SpotAtt = 1;");
        SB.AppendLine();

        SB.AppendLine("\t\tif (Lights[i].SpotAttEnb != 0) {");
        SB.AppendLine("\t\t\tfloat SpotIndex;");
        SB.AppendLine();

        SB.AppendLine("\t\t\tswitch (Lights[i].AngleLUTInput) {");
        SB.AppendLine("\t\t\tcase 0: SpotIndex = CosNormalHalf; break;");
        SB.AppendLine("\t\t\tcase 1: SpotIndex = CosViewHalf; break;");
        SB.AppendLine("\t\t\tcase 2: SpotIndex = CosNormalView; break;");
        SB.AppendLine("\t\t\tcase 3: SpotIndex = CosLightNormal; break;");
        SB.AppendLine("\t\t\tcase 4: SpotIndex = CosLightSpot; break;");
        SB.AppendLine("\t\t\tcase 5: SpotIndex = CosPhi; break;");
        SB.AppendLine("\t\t\t}");
        SB.AppendLine();

        SB.AppendLine(
            "\t\t\tSpotAtt = SampleLUT(" + "6 + i * 2, SpotIndex, Lights[i].AngleLUTScale);"
        );

        SB.AppendLine("\t\t}");
        SB.AppendLine();

        SB.AppendLine("\t\tfloat DistAtt = 1;");
        SB.AppendLine();

        SB.AppendLine("\t\tif (Lights[i].DistAttEnb != 0) {");

        string DistAttIdx;

        DistAttIdx = $"length(-{View}.xyz - RPos) * Lights[i].AttScale";
        DistAttIdx = $"clamp({DistAttIdx} + Lights[i].AttBias, 0, 1)";

        SB.AppendLine($"\t\t\tDistAtt = SampleLUT(7 + i * 2, {DistAttIdx}, 1);");
        SB.AppendLine("\t\t}");
        SB.AppendLine();

        string ClampHighLight = string.Empty;

        string Specular0Color = Specular0Uniform;
        string Specular1Color = Specular1Uniform;

        if ((Params.FragmentFlags & H3DFragmentFlags.IsClampHighLightEnabled) != 0)
        {
            ClampHighLight = " * fi";

            SB.AppendLine("\t\tfloat fi = (CosLightNormal < 0) ? 0 : 1;");
        }

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTDist0Enabled) != 0)
        {
            Specular0Color += " * d0";

            SB.AppendLine($"\t\tfloat d0 = {Dist0};");

            SB.AppendLine("\t\tDebugDist0 = vec4(d0);");
        }

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTReflectionEnabled) != 0)
        {
            Specular1Color = "r";

            SB.AppendLine("\t\tvec4 r = vec4(");
            SB.AppendLine($"\t\t\t{ReflecR},");
            SB.AppendLine($"\t\t\t{ReflecG},");
            SB.AppendLine($"\t\t\t{ReflecB}, 1);");

            SB.AppendLine("\t\tDebugSpec = r;");
        }

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTDist1Enabled) != 0)
        {
            Specular1Color += " * d1";

            SB.AppendLine($"\t\tfloat d1 = {Dist1};");

            SB.AppendLine("\t\tDebugDist1 = vec4(d1);");
        }

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTGeoFactor0Enabled) != 0)
            Specular0Color += " * g";

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTGeoFactor1Enabled) != 0)
            Specular1Color += " * g";

        if ((Params.FragmentFlags & H3DFragmentFlags.IsLUTGeoFactorEnabled) != 0)
            SB.AppendLine("\t\tfloat g = ln / abs(dot(Half, Half));");

        SB.AppendLine("\t\tvec4 Diffuse =");
        SB.AppendLine($"\t\t\t{AmbientUniform} * Lights[i].Ambient +");
        SB.AppendLine($"\t\t\t{DiffuseUniform} * Lights[i].Diffuse * clamp(ln, 0, 1);");
        SB.AppendLine(
            $"\t\tvec4 Specular = "
                + $"{Specular0Color} * Lights[i].Specular0 + "
                + $"{Specular1Color} * Lights[i].Specular1;"
        );
        SB.AppendLine($"\t\tFragPriColor.rgb += Diffuse.rgb * SpotAtt * DistAtt;");
        SB.AppendLine($"\t\tFragSecColor.rgb += Specular.rgb * SpotAtt * DistAtt{ClampHighLight};");

        if ((Params.FresnelSelector & H3DFresnelSelector.Pri) != 0)
            SB.AppendLine($"\t\tFragPriColor.a = {Fresnel};");

        if ((Params.FresnelSelector & H3DFresnelSelector.Sec) != 0)
            SB.AppendLine($"\t\tFragSecColor.a = {Fresnel};");

        if (Params.FresnelSelector != H3DFresnelSelector.No)
        {
            SB.AppendLine($"\t\tDebugFresnel = vec4({Fresnel});");
        }
        
        //Only done if a light has ConstantColor5 active 
        SB.AppendLine($"\t\t{Constant5Variable} = {Constant5Variable} * Lights[i].DisableConst5 + Lights[i].ConstantColor5 * (1 - Lights[i].DisableConst5);");

        //Lights loop end
        SB.AppendLine("\t}");
        SB.AppendLine("\tFragPriColor = clamp(FragPriColor, 0, 1);");
        SB.AppendLine("\tFragSecColor = clamp(FragSecColor, 0, 1);");
        SB.AppendLine();
    }

    private static string GetLUTInput(PICALUTInput Input, PICALUTScale Scale, int LUT)
    {
        //TODO: CosLightSpot and CosPhi
        string InputStr;

        switch (Input)
        {
            default:
            case PICALUTInput.CosNormalHalf:
                InputStr = "CosNormalHalf";
                break;
            case PICALUTInput.CosViewHalf:
                InputStr = "CosViewHalf";
                break;
            case PICALUTInput.CosNormalView:
                InputStr = "CosNormalView";
                break;
            case PICALUTInput.CosLightNormal:
                InputStr = "CosLightNormal";
                break;
            case PICALUTInput.CosLightSpot:
                InputStr = "CosLightSpot";
                break;
            case PICALUTInput.CosPhi:
                InputStr = "CosPhi";
                break;
        }

        string Output = $"texture(LUTs[{LUT}], vec2(({InputStr} + 1) * 0.5, 0)).r";

        if (Scale != PICALUTScale.One)
        {
            string ScaleStr = Scale.ToSingle().ToString(CultureInfo.InvariantCulture);

            Output = $"min({Output} * {ScaleStr}, 1)";
        }

        return Output;
    }

    private void GenTexColor(int Index)
    {
        SB.AppendLine($"\tvec4 Color{Index} = {GetTextureUniform(Index)};");
    }

    private string GetTextureUniform(int Index)
    {
        H3DTextureCoord TexCoord = Params.TextureCoords[Index];

        string Texture;

        string TexCoord0 = $"{ShaderOutputRegName.TexCoord0}";
        string TexCoord1 = $"{ShaderOutputRegName.TexCoord1}";
        string TexCoord2 = $"{ShaderOutputRegName.TexCoord2}";

        if (Index == 0)
        {
            Texture = CalculateTexture(Index, TexCoord0);
            ;
        }
        else
        {
            int CoordIndex = Index;

            if (
                CoordIndex == 2
                && (
                    Params.TexCoordConfig == H3DTexCoordConfig.Config0110
                    || Params.TexCoordConfig == H3DTexCoordConfig.Config0111
                    || Params.TexCoordConfig == H3DTexCoordConfig.Config0112
                )
            )
            {
                CoordIndex = 1;
            }

            switch (CoordIndex)
            {
                default:
                case 0:
                    Texture = CalculateTexture(Index, TexCoord0);
                    break;
                case 1:
                    Texture = CalculateTexture(Index, TexCoord1);
                    break;
                case 2:
                    Texture = CalculateTexture(Index, TexCoord2);
                    break;
            }
        }
        return Texture;
    }

    private string CalculateTexture(int Index, string TexCoord)
    {
        switch (Params.TextureCoords[Index].MappingType)
        {
            case H3DTextureMappingType.CameraCubeEnvMap:
                return $"texture(TextureCube, {TexCoord}.xyz)";
            case H3DTextureMappingType.ProjectionMap:
                return $"textureProj(Textures[{Index}], {TexCoord}.xyz)";
            default:
                return $"texture(Textures[{Index}], {TexCoord}.xy)";
        }
    }

    private void GenCombinerColor(PICATexEnvStage Stage, int id, string[] ColorArgs)
    {
        SB.AppendLine($"\t//Stage {id}");
        switch (Stage.Combiner.Color)
        {
            case PICATextureCombinerMode.Replace:
                SB.AppendLine($"\tOutput.rgb = ({ColorArgs[0]}).rgb;");
                break;
            case PICATextureCombinerMode.Modulate:
                SB.AppendLine($"\tOutput.rgb = ({ColorArgs[0]}).rgb * ({ColorArgs[1]}).rgb;");
                break;
            case PICATextureCombinerMode.Add:
                SB.AppendLine(
                    $"\tOutput.rgb = min(({ColorArgs[0]}).rgb + ({ColorArgs[1]}).rgb, 1);"
                );
                break;
            case PICATextureCombinerMode.AddSigned:
                SB.AppendLine(
                    $"\tOutput.rgb = clamp(({ColorArgs[0]}).rgb + ({ColorArgs[1]}).rgb - 0.5, 0, 1);"
                );
                break;
            case PICATextureCombinerMode.Interpolate:
                SB.AppendLine(
                    $"\tOutput.rgb = mix(({ColorArgs[1]}).rgb, ({ColorArgs[0]}).rgb, ({ColorArgs[2]}).rgb);"
                );
                break;
            case PICATextureCombinerMode.Subtract:
                SB.AppendLine(
                    $"\tOutput.rgb = max(({ColorArgs[0]}).rgb - ({ColorArgs[1]}).rgb, 0);"
                );
                break;
            case PICATextureCombinerMode.DotProduct3Rgb:
                SB.AppendLine(
                    $"\tOutput.rgb = vec3(min(dot(({ColorArgs[0]}).rgb, ({ColorArgs[1]}).rgb), 1));"
                );
                break;
            case PICATextureCombinerMode.DotProduct3Rgba:
                SB.AppendLine(
                    $"\tOutput.rgb = vec3(min(dot(({ColorArgs[0]}), ({ColorArgs[1]})), 1));"
                );
                break;
            case PICATextureCombinerMode.MultAdd:
                SB.AppendLine(
                    $"\tOutput.rgb = min(({ColorArgs[0]}).rgb * ({ColorArgs[1]}).rgb + ({ColorArgs[2]}).rgb, 1);"
                );
                break;
            case PICATextureCombinerMode.AddMult:
                SB.AppendLine(
                    $"\tOutput.rgb = min(({ColorArgs[0]}).rgb + ({ColorArgs[1]}).rgb, 1) * ({ColorArgs[2]}).rgb;"
                );
                break;
        }
    }

    private void GenCombinerAlpha(PICATexEnvStage Stage, string[] AlphaArgs)
    {
        switch (Stage.Combiner.Alpha)
        {
            case PICATextureCombinerMode.Replace:
                SB.AppendLine($"\tOutput.a = ({AlphaArgs[0]});");
                break;
            case PICATextureCombinerMode.Modulate:
                SB.AppendLine($"\tOutput.a = ({AlphaArgs[0]}) * ({AlphaArgs[1]});");
                break;
            case PICATextureCombinerMode.Add:
                SB.AppendLine($"\tOutput.a = min(({AlphaArgs[0]}) + ({AlphaArgs[1]}), 1);");
                break;
            case PICATextureCombinerMode.AddSigned:
                SB.AppendLine(
                    $"\tOutput.a = clamp(({AlphaArgs[0]}) + ({AlphaArgs[1]}) - 0.5, 0, 1);"
                );
                break;
            case PICATextureCombinerMode.Interpolate:
                SB.AppendLine(
                    $"\tOutput.a = mix(({AlphaArgs[1]}), ({AlphaArgs[0]}), ({AlphaArgs[2]}));"
                );
                break;
            case PICATextureCombinerMode.Subtract:
                SB.AppendLine($"\tOutput.a = max(({AlphaArgs[0]}) - ({AlphaArgs[1]}), 0);");
                break;
            case PICATextureCombinerMode.DotProduct3Rgb:
                SB.AppendLine(
                    $"\tOutput.a = min(dot(vec3({AlphaArgs[0]}), vec3({AlphaArgs[1]})), 1);"
                );
                break;
            case PICATextureCombinerMode.DotProduct3Rgba:
                SB.AppendLine(
                    $"\tOutput.a = min(dot(vec4({AlphaArgs[0]}), vec4({AlphaArgs[1]})), 1);"
                );
                break;
            case PICATextureCombinerMode.MultAdd:
                SB.AppendLine(
                    $"\tOutput.a = min(({AlphaArgs[0]}) * ({AlphaArgs[1]}) + ({AlphaArgs[2]}), 1);"
                );
                break;
            case PICATextureCombinerMode.AddMult:
                SB.AppendLine(
                    $"\tOutput.a = min(({AlphaArgs[0]}) + ({AlphaArgs[1]}), 1) * ({AlphaArgs[2]});"
                );
                break;
        }
    }

    private static string GetCombinerSource(PICATextureCombinerSource Source, string Constant)
    {
        switch (Source)
        {
            default:
            case PICATextureCombinerSource.PrimaryColor:
                return $"{ShaderOutputRegName.Color}";
            case PICATextureCombinerSource.FragmentPrimaryColor:
                return "FragPriColor";
            case PICATextureCombinerSource.FragmentSecondaryColor:
                return "FragSecColor";
            case PICATextureCombinerSource.Texture0:
                return "Color0";
            case PICATextureCombinerSource.Texture1:
                return "Color1";
            case PICATextureCombinerSource.Texture2:
                return "Color2";
            case PICATextureCombinerSource.PreviousBuffer:
                return "CombBuffer";
            case PICATextureCombinerSource.Constant:
                return Constant;
            case PICATextureCombinerSource.Previous:
                return "Previous";
        }
    }

    private static string GetVec4(RGBA Color)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "vec4({0}, {1}, {2}, {3})",
            Color.R / 255f,
            Color.G / 255f,
            Color.B / 255f,
            Color.A / 255f
        );
    }
}
