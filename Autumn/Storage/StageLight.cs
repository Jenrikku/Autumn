using System.Numerics;
using Autumn.Rendering.CtrH3D;
using BYAMLSharp;

namespace Autumn.Storage;


internal class LightArea
{
    public int InterpolateFrame = 10; // "Interpolate Frame"
    public string Name = "ステージデフォルトライト"; //Stage Default Light
    public StageLight MapObjectLight = new();
    public StageLight ObjectLight = new();
    public StageLight PlayerLight = new();

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> slDict = new()
        {
            { "Interpolate Frame", new(BYAMLNodeType.Int, InterpolateFrame) },
            { "MapObj Light", MapObjectLight.GetNodes() },
            { "Name", new(BYAMLNodeType.String, Name) },
            { "Obj Light", ObjectLight.GetNodes() },
            { "Player Light", PlayerLight.GetNodes() },
        };

        return new(slDict);
    }
}
internal class LightParams
{
    public int InterpolateFrame = 10; // "Interpolate Frame"
    public string Name = "ステージデフォルトライト"; //Stage Default Light
    public StageLight MapObjectLight = new();
    public StageLight ObjectLight = new();
    public StageLight PlayerLight = new();
    public StageLight StageMapLight = new();

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> rd = new()
        {
            {"Stage Map Light", StageMapLight.GetNodes()}
        };

        Dictionary<string, BYAMLNode> slDict = new()
        {
            { "Interpolate Frame", new(BYAMLNodeType.Int, InterpolateFrame) },
            { "MapObj Light", MapObjectLight.GetNodes() },
            { "Name", new(BYAMLNodeType.String, Name) },
            { "Obj Light", ObjectLight.GetNodes() },
            { "Player Light", PlayerLight.GetNodes() },
        };
        rd.Add("Stage Light", new(slDict));

        return new(rd);
    }
}

internal class StageLight
{
    public Vector4?[] ConstantColors = new Vector4?[6];
    public Vector4 Ambient = new(0.23f, 0.22f, 0.21f, 1);
    public Vector4 Diffuse = new(0.23f, 0.22f, 0.21f, 1);
    public Vector4 Specular0 = new(0.5f, 0.5f, 0.5f, 1);
    public Vector4 Specular1 = new(0.5f, 0.5f, 0.5f, 1);
    public bool IsCameraFollow = false;
    public Vector3 Direction = new(-1,-1,-0.7f);

    public StageLight()
    {
    }

    public StageLight(StageLight l)
    {
        
        for( int i = 0; i < 6; i++)
        {
            ConstantColors[i] = l.ConstantColors[i];
        }
        Ambient = l.Ambient;
        Diffuse = l.Diffuse;
        Specular0 = l.Specular0;
        Specular1 = l.Specular1;
        IsCameraFollow = l.IsCameraFollow;
        Direction = l.Direction;
    }

    public StageLight(Dictionary<string, BYAMLNode> dict)
    {
        dict.TryGetValue("Ambient.a",       out BYAMLNode Aa);          
        dict.TryGetValue("Ambient.b",       out BYAMLNode Ab);          
        dict.TryGetValue("Ambient.g",       out BYAMLNode Ag);
        dict.TryGetValue("Ambient.r",       out BYAMLNode Ar);
        dict.TryGetValue("Diffuse.a",       out BYAMLNode Da);          
        dict.TryGetValue("Diffuse.b",       out BYAMLNode Db);
        dict.TryGetValue("Diffuse.g",       out BYAMLNode Dg);          
        dict.TryGetValue("Diffuse.r",       out BYAMLNode Dr);
        dict.TryGetValue("Direction.x",     out BYAMLNode Dx);            
        dict.TryGetValue("Direction.y",     out BYAMLNode Dy);
        dict.TryGetValue("Direction.z",     out BYAMLNode Dz);
        dict.TryGetValue("IsCameraFollow",  out BYAMLNode CamFollow);
        dict.TryGetValue("Specular0.a",     out BYAMLNode S0a);        
        dict.TryGetValue("Specular0.b",     out BYAMLNode S0b);
        dict.TryGetValue("Specular0.g",     out BYAMLNode S0g);
        dict.TryGetValue("Specular0.r",     out BYAMLNode S0r);
        dict.TryGetValue("Specular1.a",     out BYAMLNode S1a);
        dict.TryGetValue("Specular1.b",     out BYAMLNode S1b);
        dict.TryGetValue("Specular1.g",     out BYAMLNode S1g);
        dict.TryGetValue("Specular1.r",     out BYAMLNode S1r);

        Ambient = new(Ar?.GetValueAs<float>() ?? 1, Ag?.GetValueAs<float>() ?? 1, Ab?.GetValueAs<float>() ?? 1, Aa?.GetValueAs<float>() ?? 1); 
        Diffuse = new(Dr?.GetValueAs<float>() ?? 1, Dg?.GetValueAs<float>() ?? 1, Db?.GetValueAs<float>() ?? 1, Da?.GetValueAs<float>() ?? 1); 
        Direction = new(Dx?.GetValueAs<float>() ?? 1, Dy?.GetValueAs<float>() ?? 1, Dz?.GetValueAs<float>() ?? 1); 
        Specular0 = new(S0r?.GetValueAs<float>() ?? 1, S0g?.GetValueAs<float>() ?? 1, S0b?.GetValueAs<float>() ?? 1, S0a?.GetValueAs<float>() ?? 1); 
        Specular1 = new(S1r?.GetValueAs<float>() ?? 1, S1g?.GetValueAs<float>() ?? 1, S1b?.GetValueAs<float>() ?? 1, S1a?.GetValueAs<float>() ?? 1);
        IsCameraFollow = CamFollow?.GetValueAs<bool>() ?? true;

        for (int i = 0; i < 6; i++)
        {
            dict.TryGetValue($"ConstantColor{i}.a", out BYAMLNode CCa);
            dict.TryGetValue($"ConstantColor{i}.b", out BYAMLNode CCb);
            dict.TryGetValue($"ConstantColor{i}.g", out BYAMLNode CCg);
            dict.TryGetValue($"ConstantColor{i}.r", out BYAMLNode CCr);
            if (CCa == null || CCb == null || CCg == null || CCr == null)
                continue;
            ConstantColors[i] = new(CCr?.GetValueAs<float>() ?? 1, CCg?.GetValueAs<float>() ?? 1, CCb?.GetValueAs<float>() ?? 1, CCa?.GetValueAs<float>() ?? 1);
        }
        
    }

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> rd = new()
        {
            { "Ambient.a", new(BYAMLNodeType.Float, Ambient.W) },
            { "Ambient.b", new(BYAMLNodeType.Float, Ambient.Z) },
            { "Ambient.g", new(BYAMLNodeType.Float, Ambient.Y) },
            { "Ambient.r", new(BYAMLNodeType.Float, Ambient.X) },
            { "Diffuse.a", new(BYAMLNodeType.Float, Diffuse.W) },
            { "Diffuse.b", new(BYAMLNodeType.Float, Diffuse.Z) },
            { "Diffuse.g", new(BYAMLNodeType.Float, Diffuse.Y) },
            { "Diffuse.r", new(BYAMLNodeType.Float, Diffuse.X) },
            { "Direction.x", new(BYAMLNodeType.Float, Direction.X) },
            { "Direction.y", new(BYAMLNodeType.Float, Direction.Y) },
            { "Direction.z", new(BYAMLNodeType.Float, Direction.Z) },
            { "IsCameraFollow", new(BYAMLNodeType.Bool, IsCameraFollow) },
            { "Specular0.a", new(BYAMLNodeType.Float, Specular0.W) },
            { "Specular0.b", new(BYAMLNodeType.Float, Specular0.Z) },
            { "Specular0.g", new(BYAMLNodeType.Float, Specular0.Y) },
            { "Specular0.r", new(BYAMLNodeType.Float, Specular0.X) },
            { "Specular1.a", new(BYAMLNodeType.Float, Specular1.W) },
            { "Specular1.b", new(BYAMLNodeType.Float, Specular1.Z) },
            { "Specular1.g", new(BYAMLNodeType.Float, Specular1.Y) },
            { "Specular1.r", new(BYAMLNodeType.Float, Specular1.X) },
        };
        for (int i = 0; i < 6; i++)
        {
            // if (ConstantColors[i] == null)
            //     ConstantColors[i] = new(0, 0, 1, 1); 
            if (ConstantColors[i] != null)
            {
                rd.Add($"ConstantColor{i}.a", new(BYAMLNodeType.Float, ((Vector4)ConstantColors[i]!).W));
                rd.Add($"ConstantColor{i}.b", new(BYAMLNodeType.Float, ((Vector4)ConstantColors[i]!).Z));
                rd.Add($"ConstantColor{i}.g", new(BYAMLNodeType.Float, ((Vector4)ConstantColors[i]!).Y));
                rd.Add($"ConstantColor{i}.r", new(BYAMLNodeType.Float, ((Vector4)ConstantColors[i]!).X));
            }
        }

        return new(rd);
    }

    public H3DRenderingMaterial.Light GetAsLight()
    {
        return new()
        {
            Position = Direction,
            Direction = new(),
            Ambient = Ambient,
            Diffuse = Diffuse,
            Specular0 = Specular0,
            Specular1 = Specular1,
            TwoSidedDiffuse = 0, // bool
            Directional = IsCameraFollow ? 1 : 0 , // bool
            ConstantColor5 = ConstantColors[5] ?? new Vector4(0,0,0,0),
            DisableConst5 = ConstantColors[5] == null ? 1 : 0
        };
    }
}