
namespace Autumn.Storage;

internal class ActorLight
{
    public CalcType Calc = CalcType.Initial;
    public LightType Type = LightType.Terrain; // DEFAULT 

    /// <summary>
    /// Determines when the light gets calculated for this actor 
    ///
    /// </summary>
    public enum CalcType
    {
        /// <summary> Light is only calculated when the actor first shows up - 出現時のみ </summary>
        Initial,

        /// <summary> Light is calculated every frame - いつも計算 </summary>
        Continuous
    }
    
    /// <summary>
    /// Type of light that will affect this actor
    /// </summary>
    public enum LightType
    {
        Player, //プレイヤー,
        Character, //キャラ,
        Terrain, // 地形, // DEFAULT 
        Terrain_Object, // Stage Object  // 地形オブジェ,
        /// <summary>地形_コンスト５, can take constant5 in its light calculation</summary>
        Terrain_Constant5, //
    }
    public void GetCalcType(string s)
    {
        Calc = s switch
        {
            "出現時のみ" => CalcType.Initial,
            "いつも計算" => CalcType.Continuous
        };
    }

    public void GetType(string s)
    {
        Type = s switch
        {
            "プレイヤー"    => LightType.Player,
            "キャラ"    => LightType.Character,
            "地形"  => LightType.Terrain,
            "地形オブジェ"  => LightType.Terrain_Object,
            "地形_コンスト５"   => LightType.Terrain_Constant5
        };
    }

}