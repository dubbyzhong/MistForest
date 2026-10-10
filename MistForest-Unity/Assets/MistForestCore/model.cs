namespace MistForest.Core.Model
{
    // the model of map, which describes the length of the map.
    public enum Mode
    {
        Scout = 3,
        Heroes = 5,
    }

    // the terrain of the map's grids.
    public enum Terrain
    {
        Ground, // can stand.
        Wall, // cannot stand and stop attack.
        Lake, // cannot stand but allow attack
        Whirlpool, // snap the unit above (could stand.)
    }

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
        Flamer,
        WallGuard,
        Prince,
        Sword,
    }

    public enum Kind
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

    public sealed class Map
    {
        public int Width = 5;
        public int Height = 5;
        public Terrain[] Cells = new Terrain[9 * 9];

        public bool[] HorizontalWalls = new bool[90];
        public bool[] VerticalWalls = new bool[90];
    }

    public static class MapInfo
    {
        public static int Size(Mode mode)
        {
            int code = (int)mode;
            return code == 3 ? 3
                : code == 5 ? 5
                : code / 10;
        }

        public static int Rows(Mode mode)
        {
            int code = (int)mode;
            return code == 3 ? 3
                : code == 5 ? 5
                : code % 10;
        }

        public static int CellCount(Mode mode) => Size(mode) * Rows(mode);
    }
}
