using System.Numerics;
using BYAMLSharp;

namespace Autumn.Storage;

internal class StageFog
{
    public Vector3 Color = new(1);
    public float Density = 0;
    public FogTypes FogType = FogTypes.FOG_UPDATER_TYPE_LINEAR;
    
    public enum FogTypes
    {
        FOG_UPDATER_TYPE_LINEAR,
        FOG_UPDATER_TYPE_EXPONENT,
        FOG_UPDATER_TYPE_EXPONENT_SQUARE,
    }

    public int InterpFrame = 0;
    public float MaxDepth = 0;
    public float MinDepth = 30000;
    public int AreaId = -1; // -1 reserved to the main stage fog, everything else used on FogAreas  

    public StageFog() 
    {
    }
    public StageFog(StageFog stageFog)
    {
        Color = stageFog.Color;
        Density = stageFog.Density;
        InterpFrame = stageFog.InterpFrame;
        MaxDepth = stageFog.MaxDepth;
        MinDepth = stageFog.MinDepth;
    }

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> rd = new()
        {
            { "ColorB", new(BYAMLNodeType.Float, Color.Z) },
            { "ColorG", new(BYAMLNodeType.Float, Color.Y) },
            { "ColorR", new(BYAMLNodeType.Float, Color.X) },
            { "Density", new(BYAMLNodeType.Float, Density) },
            { "InterpFrame", new(BYAMLNodeType.Int, InterpFrame) },
            { "Area Id", new(BYAMLNodeType.Int, AreaId) },
            { "MaxDepth", new(BYAMLNodeType.Float, MaxDepth) },
            { "MinDepth", new(BYAMLNodeType.Float, MinDepth) }
        };

        return new(rd);
    }
}