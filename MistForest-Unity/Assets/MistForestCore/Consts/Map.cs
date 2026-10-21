namespace MistForest.Core.Consts
{
    public class MapConsts
    {
        public const int WhirlAttachDistance = 1;
        public const int WhirlpoolImmuneExpire = 1;
        public const bool DiagonalBlocked = true;
        public const int FortHealth = 1;
        public const int WallHealth = 1;
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
