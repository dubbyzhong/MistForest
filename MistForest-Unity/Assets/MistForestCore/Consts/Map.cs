namespace MistForest.Core.Consts
{
    public class MapConsts
    {
        public const int WhirlAttachDistance = 1;
        public const int WhirlpoolImmuneExpire = 1;
        public const bool DiagonalBlocked = true;
        /// <summary>
        /// Default fort health in quarter-point units, equivalent to one displayed health point.
        /// </summary>
        public const int FortHealth = CombatConsts.UnitsPerPoint;
        /// <summary>
        /// Default wall health in quarter-point units, equivalent to one displayed health point.
        /// </summary>
        public const int WallHealth = CombatConsts.UnitsPerPoint;
        public const int RubbleExpireRound = 1;
        public static (int dx, int dy)[] MoveManhattan = { (-1, 0), (1, 0), (0, -1), (0, 1) };
        public static (int dx, int dy)[] MoveChebyshev =
        {
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1),
            (-1, -1),
            (-1, 1),
            (1, -1),
            (1, 1),
        };
    }
}
