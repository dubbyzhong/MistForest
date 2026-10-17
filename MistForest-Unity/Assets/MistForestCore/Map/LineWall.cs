using MistForest.Core.Utils;

namespace MistForest.Core.GameMap
{
    public class LineWalls : IClone<LineWalls>
    {
        private Bitset _inner;

        public int Capacity => _inner.Capacity;
        public ulong[] Inner => _inner.Words;

        public bool this[int index] => _inner[index];

        private LineWalls(Bitset inner) => _inner = inner;

        public static LineWalls WithCapacity(int capacity)
        {
            return new(Bitset.WithCapacity(capacity));
        }

        public LineWalls Clone()
        {
            return new LineWalls(_inner.Clone());
        }

        public bool SetInner(int index, ulong value, out string? error)
        {
            error = null;
            return _inner.SetInner(index, value, out error);
        }
    }
}
