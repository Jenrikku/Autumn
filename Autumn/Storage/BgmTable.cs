namespace Autumn.Storage;

internal class BgmTable
{
    enum StageBgmDefaultKinds
    {
        /// <summary>
        /// Default stage music, overrides whatever was defined in StageDefaultBgm
        /// </summary>
        Stage,

        /// <summary>
        /// Used on BgmChangeArea, user defined.
        /// </summary>
        OutSide,

        /// <summary>
        /// Used on Bowser fights.
        /// </summary>
        Boss,

        /// <summary>
        /// Used after beating any boss.
        /// </summary>
        AfterBattle,

        /// <summary>
        /// Used on BoomBoom and PomPom fights.
        /// </summary>
        PunPun
    }

    /// <summary>
    /// Defines the song used in a specific stage (NameScenario -> FirstStage_1) by default. <br/>
    /// SoundData/BgmTable.szs/StageDefaultBgmList.byml
    /// </summary>
    public class StageDefaultBgm
    {
        public int Scenario = 1;
        public string StageName = "FirstStage";
        public string BgmLabel = "STM_BGM_FIELD_WALK";

        // public StageDefaultBgm(string name, byte sc, string lbl)
        // {
        //     Scenario = sc;
        //     StageName = name;
        //     BgmLabel = lbl;
        // }
    }

    /// <summary>
    /// Bgm type that can be modified to add new types, used in
    /// BgmChangeArea Arg0. <br/>
    /// SoundData/BgmTable.szs/StageBgmList.byml -> KindNumList
    /// </summary>
    public List<string> BgmTypes = new();

    /// <summary>
    /// Offers more granularity over the stage's music, using songs determined by the <see cref="BgmTypes"/><br/>
    /// SoundData/BgmTable.szs/StageBgmList.byml -> StageBgmList
    /// </summary>
    public class StageBgm
    {
        public int? Scenario = 1;
        public string StageName = "FirstStage";

        /// <summary>
        /// Structured like:
        /// Linelist/Line[]/KindDefine1, KindDefine2...
        /// </summary>
        public Dictionary<string, List<KindDefine>> LineList = new();
    }

    public class KindDefine
    {
        /// <summary>
        /// Track type as defined by BgmType or one of the internal types.
        /// </summary>
        public string Kind = "OutSide";

        /// <summary>
        /// The track name within the game files.
        /// </summary>
        public string Label = "STM_BGM_FIELD_WALK";
    }

    public List<StageDefaultBgm> StageDefaultBgmList = new();
    public List<StageBgm> StageBgmList = new();
    public string[] BgmFiles = [];
}
