using System;

namespace MistForest.Core.Utils
{
    public sealed class Box<T>
    {
        private T? _inner;

        public static Box<T> Empty => new() { _inner = default };

        public static Box<T> New(T inner)
        {
            return new() { _inner = inner };
        }

        public Box<U> Map<U>(Func<T, U> mapper)
        {
            if (_inner is not T innerValue)
            {
                return Box<U>.Empty;
            }
            return Box.New(mapper(innerValue));
        }

        public static implicit operator T?(Box<T> box) => box._inner;

        public T? TryTake()
        {
            if (_inner is not T innerValue)
            {
                return default;
            }
            _inner = default;
            return innerValue;
        }

        public T Take() => TryTake()!;
    }

    public sealed class Box
    {
        public static Box<T> New<T>(T inner)
        {
            return Box<T>.New(inner);
        }

        public static Box<T> From<T>(ref T? inner)
        {
            Box<T> box;
            if (inner is null)
            {
                box = Box<T>.Empty;
            }
            else
            {
                box = Box<T>.New(inner);
            }
            inner = default;
            return box;
        }
    }
}
