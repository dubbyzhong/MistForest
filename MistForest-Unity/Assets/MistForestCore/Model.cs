namespace MistForest.Core.Model
{
    /// <summary>
    /// the model of map, which describes the length of the map.
    /// </summary>
    public enum Mode
    {
        Scout = 3,
        Heroes = 5,
    }

    /// <summary>
    /// the terrain of the map's grids.
    /// </summary>
    public enum Terrain
    {
        Ground, // can stand.
        Wall, // cannot stand and stop attack.
        Lake, // cannot stand but allow attack
        Whirlpool, // snap the unit above (could stand.)
    }

    /// <summary>
    /// the roles which player can choose.
    /// </summary>
    public enum Role
    {
        Sniper,
        Wolf,
        Ink,
        Shadow,
        Heavy,
        HiddenWolf,
        Knight,
        Alpha,
        Armiger,
        Firefly,
        WallGuard,
        Prince,
        Sword,
    }

    /// <summary>
    /// the kinds of actions which player can do.
    /// </summary>
    public enum ActionKind
    {
        Move,
        Attack,
        Row,
        Col,
        Dash,
        Follow,
        Lead,
        Charge,
        Transform,
        Leap,
        Hunt,
        Silence,
        Cleave,
        CleaveRush,
        Retreat,
        BuildFort,
        BuildWall,
        AttackRush,
        Blood,
        ArmorUp,
        ArmorDown,
        Thrust,
        Flame,
        FireStep,
        FlameAdvance,
        Demolish,
        Surrender,
        Share,
        ShadowMode,
        PlantRose,
        Bloom,
        SwordStab,
        ThrowSword,
        RecallSword,
        SwordAdvance,
        End,
    }

    /// <summary>
    /// the map of the game.
    /// </summary>
    public sealed class Map
    {
        public int Width = 5;
        public int Height = 5;
        public Terrain[] Cells = new Terrain[9 * 9];

        public bool[] HorizontalWalls = new bool[72];
        public bool[] VerticalWalls = new bool[72];
    }

    /// <summary>
    /// the information of the map.
    /// </summary>
    public static class MapInfo
    {
        /// <summary>
        /// calculate the rows of the map through <c>mode</c>.
        /// If <c>mode</c> is <c>Mode.Scout</c>, then the size is 3.
        /// If <c>mode</c> is <c>Mode.Heroes</c>, then the size is 5.
        /// And if none of them, then the rows is <c>mode / 10</c>.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static int Rows(Mode mode)
        {
            int code = (int)mode;
            return code == 3 ? 3
                : code == 5 ? 5
                : code / 10;
        }

        /// <summary>
        /// calculate the columns of the map through <c>mode</c>.
        /// If <c>mode</c> is <c>Mode.Scout</c>, then the size is 3.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static int Cols(Mode mode)
        {
            int code = (int)mode;
            return code == 3 ? 3
                : code == 5 ? 5
                : code % 10;
        }

        /// <summary>
        /// calculate the total cell count of the map through <c>mode</c>.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static int CellCount(Mode mode) => Rows(mode) * Cols(mode);
    }
}
