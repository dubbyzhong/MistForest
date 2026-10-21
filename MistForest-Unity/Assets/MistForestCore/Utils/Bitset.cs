using System;

namespace MistForest.Core.Utils
{
    public sealed class Bitset : IClone<Bitset>
    {
        private int _capacity;
        private readonly ulong[] _words;

        public int Capacity => _capacity;
        public int Count => (_capacity - 1) / (8 * sizeof(ulong)) + 1;
        public ulong[] Words => _words;

        private Bitset(ulong[] words) => _words = words;

        public static Bitset WithCapacity(int capacity)
        {
            return new(words: new ulong[(capacity - 1) / (8 * sizeof(ulong)) + 1])
            {
                _capacity = capacity,
            };
        }

        public Bitset Clone()
        {
            return new(words: (ulong[])_words.Clone()) { _capacity = _capacity };
        }

        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        index,
                        "Index out of range"
                    );
                }
                return (
                        _words[index / (8 * sizeof(ulong))] & (1ul << (index % (8 * sizeof(ulong))))
                    ) != 0;
            }
            set
            {
                if (index < 0 || index >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        index,
                        "Index out of range"
                    );
                }
                if (value)
                {
                    _words[index / (8 * sizeof(ulong))] |= 1ul << (index % (8 * sizeof(ulong)));
                }
                else
                {
                    _words[index / (8 * sizeof(ulong))] &= ~(1ul << (index % (8 * sizeof(ulong))));
                }
            }
        }

        public bool SetInner(int index, ulong value, out string? error)
        {
            error = null;
            if (index < 0 || index >= Count)
            {
                error = "Index out of range";
                return false;
            }
            _words[index] = value;
            return true;
        }
    }
}
