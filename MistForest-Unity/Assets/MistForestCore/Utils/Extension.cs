using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ZLinq;

namespace MistForest.Core.Utils
{
    public static class SpanExtension
    {
        public static SpanEnumerable<T> AsEnumerable<T>(this Span<T> span)
        {
            return SpanEnumerable<T>.New(span);
        }

        public static SpanEnumerable<T> AsEnumerable<T>(this ReadOnlySpan<T> span)
        {
            return SpanEnumerable<T>.New(span);
        }

        public ref struct SpanEnumerable<T>
        {
            private ReadOnlySpan<T> _span;
            private Func<T, bool>? _wherePredicate;

            public static SpanEnumerable<T> New(ReadOnlySpan<T> span)
            {
                return new SpanEnumerable<T>() { _span = span };
            }

            public SpanEnumerable<T> Where(Func<T, bool> predicate)
            {
                _wherePredicate = predicate;
                return this;
            }

            public readonly T? FirstOrDefault(Func<T, bool> predicate)
            {
                foreach (T item in this)
                {
                    if (predicate(item))
                    {
                        return item;
                    }
                }
                return default;
            }

            public readonly T First(Func<T, bool> predicate)
            {
                return FirstOrDefault(predicate)
                    ?? throw new InvalidOperationException("Sequence contains no elements");
            }

            public readonly bool Any(Func<T, bool> predicate)
            {
                foreach (T item in this)
                {
                    if (predicate(item))
                    {
                        return true;
                    }
                }
                return false;
            }

            public readonly bool All(Func<T, bool> predicate)
            {
                foreach (T item in this)
                {
                    if (predicate(item) is false)
                    {
                        return false;
                    }
                }
                return true;
            }

            public readonly T[] ToArray()
            {
                T[] buffer = ArrayPool<T>.Shared.Rent(16);
                int count = 0;

                try
                {
                    var enumerator = GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        if (count == buffer.Length)
                        {
                            T[] newBuffer = ArrayPool<T>.Shared.Rent(buffer.Length * 2);
                            Array.Copy(buffer, newBuffer, count);
                            bool needClear = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                            ArrayPool<T>.Shared.Return(buffer, needClear);
                            buffer = newBuffer;
                        }
                        buffer[count] = enumerator.Current;
                        ++count;
                    }
                    if (count == 0)
                    {
                        return Array.Empty<T>();
                    }
                    T[] result = new T[count];
                    Array.Copy(buffer, result, count);
                    return result;
                }
                finally
                {
                    bool needClear = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                    ArrayPool<T>.Shared.Return(buffer, needClear);
                }
            }

            public readonly Enumerator GetEnumerator()
            {
                return Enumerator.New(_span, _wherePredicate);
            }

            public ref struct Enumerator
            {
                private ReadOnlySpan<T> _span;
                private Func<T, bool>? _wherePredicate;
                private int _index;

                public static Enumerator New(ReadOnlySpan<T> span, Func<T, bool>? wherePredicate)
                {
                    return new Enumerator()
                    {
                        _span = span,
                        _wherePredicate = wherePredicate,
                        _index = -1,
                    };
                }

                public readonly T Current => _span[_index];

                public bool MoveNext()
                {
                    do
                    {
                        ++_index;
                    } while (
                        _index < _span.Length && _wherePredicate?.Invoke(_span[_index]) is false
                    );
                    return _index < _span.Length;
                }
            }
        }
    }

    public static class RangeExtension
    {
        public static ValueEnumerable<ZLinq.Linq.FromRange, int> AsValueEnumerable(this Range range)
        {
            int start = range.Start.Value;
            int end = range.End.Value;

            int count = Math.Max(0, end - start);

            return ValueEnumerable.Range(start, count);
        }
    }

    public static class ZLinqExtension
    {
        public static ValueEnumerable<
            ZLinq.Linq.Select2<TEnumerator, T, (T item, int index)>,
            (T item, int index)
        > WithIndex<TEnumerator, T>(this ValueEnumerable<TEnumerator, T> enumerable)
            where TEnumerator : struct, IValueEnumerator<T>
        {
            return enumerable.Select((item, index) => (item, index));
        }
    }
}
