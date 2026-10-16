using System;

namespace MistForest.Core.Utils
{
    public sealed class Bitset
    {
        private readonly ulong[] _words;
        private readonly int _capacity;

        public int Capacity => _capacity;
        public ulong[] Words => _words;

        private Bitset(int capacity)
        {
            _capacity = capacity;
            _words = new ulong[(capacity - 1) / sizeof(ulong) + 1];
        }

        public static Bitset WithCapacity(int capacity)
        {
            return new Bitset(capacity);
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
                return (_words[index / sizeof(ulong)] & (1ul << (index % sizeof(ulong)))) != 0;
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
                    _words[index / sizeof(ulong)] |= 1ul << (index % sizeof(ulong));
                }
                else
                {
                    _words[index / sizeof(ulong)] &= ~(1ul << (index % sizeof(ulong)));
                }
            }
        }

        public bool SetInner(int index, ulong value, out string? error)
        {
            error = null;
            if (index >= (_capacity - 1) / sizeof(ulong) + 1)
            {
                error = "Index out of range";
                return false;
            }
            _words[index] = value;
            return true;
        }
    }
}
