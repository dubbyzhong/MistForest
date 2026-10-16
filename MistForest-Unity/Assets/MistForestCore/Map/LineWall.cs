using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    public readonly record struct LineWalls
    {
        private readonly Bitset _inner;

        public readonly int Capacity => _inner.Capacity;
        public readonly ulong[] Inner => _inner.Words;

        public bool this[int index] => _inner[index];

        private LineWalls(Bitset inner)
        {
            _inner = inner;
        }

        public static LineWalls WithCapacity(int capacity)
        {
            return new LineWalls(Bitset.WithCapacity(capacity));
        }

        public readonly bool SetInner(int index, ulong value, out string? error)
        {
            error = null;
            return _inner.SetInner(index, value, out error);
        }
    }
}
