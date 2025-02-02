namespace Autumn.Storage;

internal class SystemDataTable
{
    public List<WorldDefine> WorldList = new();

    public class StageDefine
    {
        public StageTypes StageType = StageTypes.Normal;
        public int CollectCoinNum = -1;
        public string Miniature = "MiniatureFirst";
        public int Scenario = 1;
        public string Stage = "FirstStage";

        /// <summary>
        /// Editor only property to obtain the stage number (W-X) in a given world, obtained during read, not counting pipes or empty spaces.
        /// </summary>
        public int StageNumber = -1;
    }

    public class WorldDefine
    {
        public WorldTypes WorldType = WorldTypes.World;
        public List<StageDefine> StageList = new();

        public bool Contains(string stageName, int scenario)
        {
            return StageList.Where(x => x.Stage == stageName && x.Scenario == scenario).Any();
        }

        public string StageNumber(string stageName, int scenario)
        {
            return (
                StageList.FindIndex(x => x.Stage == stageName && x.Scenario == scenario)
                - StageList.Where(x => x.StageType == StageTypes.Empty).Count()
            ).ToString();
        }
    }

    public enum WorldTypes
    {
        World,
        Special
    }

    public enum StageTypes
    {
        Normal,
        MysteryBox,
        KinopioHousePresent,
        KinopioHouseAlbum,
        KoopaFortress,
        KoopaCastle,
        KoopaBattleShip,
        Championship,
        Dokan,
        Empty
    }
}
