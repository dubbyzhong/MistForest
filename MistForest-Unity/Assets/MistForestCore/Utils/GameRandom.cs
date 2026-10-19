using MistForest.Core.Consts;

namespace MistForest.Core.Utils
{
    public struct GameRandom
    {
        private ulong _seed;

        private GameRandom(ulong seed) => _seed = seed;

        public static GameRandom? New(ulong seed)
        {
            if (seed == 0)
            {
                seed = GameConsts.DefaultSeed;
            }
            return new(seed);
        }

        private ulong NextULong()
        {
            ulong x = _seed;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _seed = x;
            return x;
        }

        public int? NextInt(int min, int max)
        {
            if (min >= max)
                return null;
            return ((int)NextULong() % (max - min)) + min;
        }
    }
}
