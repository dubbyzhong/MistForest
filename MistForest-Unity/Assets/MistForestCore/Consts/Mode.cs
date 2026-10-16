namespace MistForest.Core.Consts
{
    public static class ModeConsts
    {
        public const int ScoutLength = 3;
        public const int HeroesLen = 5;

        private static readonly (int, int)[] _heroesWallPos =
        {
            (1, 1),
            (1, 5),
            (3, 3),
            (5, 1),
            (5, 5),
        };

        public static readonly (int X, int Y)[] HeroesWallPos = _heroesWallPos;
    }
}
