
using System.Numerics;
using BYAMLSharp;

namespace Autumn.Storage;

internal class ActorShadow
{
    public string Category = ""; // ?

    public string? ActorJointName;// = "Dokan";
    public ExCategory? ExecCategory;// = ExCategory.ShadowVolume;
    public string? ModelArcName;// = "ObjectData/ShadowVolumeDokan";
    public string Name = "Body";
    public Vector3 Offset = Vector3.UnitY;
    public Vector3 RotateOffset = Vector3.Zero;
    public float? ShadowOffset = 0; // What, Kuribo
    public Vector3 Size = Vector3.One * 100;
    public VolumeType Type;// = VolumeType.Cube; // DEFAULT 
    public string? VanishingPointShadowName;// = "Body"; // Ghostplayer, KuriboTailBig
    public bool? UseActorTransRef;// = null; // GororiBig, KoopaSwitch


    // Seen types 3 -> bunbun
    // 6 ->  AssistItem
    // 8 -> BlockNote
    // 7 Coin, CoinRing
    public enum VolumeType
    {
        Cube = -1 | 7,
        Cone = 3,
        Pyramid = 4,
        Sphere = 5,
        Cylinder = 6,
        Block = 8,
        /// <summary>
        /// Gets the model from ModelArcName
        /// </summary>
        ArchiveModel
    }
    internal static string GetShadowVolumeString(VolumeType tp)
    {
        return tp switch 
        {
            VolumeType.Pyramid => "ShadowVolumePyramid",
            VolumeType.Sphere => "ShadowVolumeSphere",
            VolumeType.Cylinder => "ShadowVolumeCylinder",
            VolumeType.Cone => "ShadowVolumeCone",
            VolumeType.Block => "ShadowVolumeBlock",
            _ => "ShadowVolumeCube",

        };
    }
    public string GetShadowVolumeString()
    {
        return Type switch 
        {
            VolumeType.ArchiveModel => ModelArcName ?? "",
            _ => GetShadowVolumeString(Type),

        };
    }
    public enum ExCategory
    {
        ShadowVolume,
        PlayerShadowVolume
    }

    public void GetVolumeType(string s)
    {
        try
        {
            Type = s switch
            {
                "四角柱" => VolumeType.Cube,
                "円柱" => VolumeType.Cylinder,
                "円錐" => VolumeType.Cone,
                "球" => VolumeType.Sphere,
                "四角錐" => VolumeType.Pyramid,
                "ブロック用" => VolumeType.Block,
                "任意モデル" => VolumeType.ArchiveModel,
                _ => throw new NotImplementedException("Unknown VolumeType")
            };
        }
        catch (NotImplementedException e) 
        {
            Console.WriteLine($"{e.Message}: {s}");
        }
    }
    public void GetExecCategory(string s)
    {
        try
        {
            ExecCategory = s switch
            {
                "影ボリューム" => ExCategory.ShadowVolume,
                "プレイヤー影ボリューム" => ExCategory.PlayerShadowVolume,
                _ => throw new NotImplementedException("Unknown ExecCategory")
            };
        }
        catch (NotImplementedException e)
        {
            Console.WriteLine($"{e.Message}: {s}");
        }
    }

    public ActorShadow(Dictionary<string, BYAMLNode> prop)
    {
        if (prop.TryGetValue("TypeName", out BYAMLNode? BYMLVal))
            GetVolumeType(BYMLVal.GetValueAs<string>()!);
        else if (prop.TryGetValue("Type", out BYMLVal))
            Type = (VolumeType)BYMLVal.GetValueAs<int>();


        if (prop.TryGetValue("ExecCategory", out BYMLVal))
            GetExecCategory(BYMLVal.GetValueAs<string>()!);

        if (prop.TryGetValue("Name", out BYMLVal))
            Name = BYMLVal.GetValueAs<string>()!;
        if (prop.TryGetValue("ModelArcName", out BYMLVal))
            ModelArcName = BYMLVal.GetValueAs<string>()!["ObjectData/".Length..];
        if (prop.TryGetValue("Offset", out BYMLVal))
            Offset = DictToVec3(BYMLVal.GetValueAs<Dictionary<string,BYAMLNode>>()) ?? Offset;
        if (prop.TryGetValue("RotateOffset", out BYMLVal))
            RotateOffset = DictToVec3(BYMLVal.GetValueAs<Dictionary<string,BYAMLNode>>()) ?? RotateOffset;
        if (prop.TryGetValue("Size", out BYMLVal))
            Size = DictToVec3(BYMLVal.GetValueAs<Dictionary<string,BYAMLNode>>()) ?? Size;

    }
    public static Vector3? DictToVec3(Dictionary<string, BYAMLNode>? dict)
    {
        if (dict is null) return null;
        dict.TryGetValue("X", out BYAMLNode x);
        dict.TryGetValue("Y", out BYAMLNode y);
        dict.TryGetValue("Z", out BYAMLNode z);
        return new(float.Round(x?.GetValueAs<float>() ?? 1, 2),
        float.Round(y?.GetValueAs<float>() ?? 1, 2),
        float.Round(z?.GetValueAs<float>() ?? 1, 2));
    }

}