using MistForest.Core.Utils;
using Xunit;
using ZLinq;

namespace MistForest.Core.Tests.Refactored.Utils
{
    public class SpanExtensionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void SpanEnumerable_ToArray_CollectsFilteredItemsBeyondInitialBuffer(bool readOnly)
        {
            int[] source = Enumerable.Range(0, 128).ToArray();

            SpanExtension.SpanEnumerable<int> enumerable = readOnly
                ? ((ReadOnlySpan<int>)source).AsEnumerable()
                : source.AsSpan().AsEnumerable();
            int[] result = enumerable
                .Where(value => value % 2 == 0)
                .ToArray();

            Assert.Equal(Enumerable.Range(0, 64).Select(value => value * 2), result);
            Assert.Equal(Enumerable.Range(0, 128), source);
        }

        [Fact]
        public static void SpanEnumerable_First_ThrowsForMissingValueTypeElement()
        {
            int[] source = { 1, 2, 3 };

            Assert.Equal(2, source.AsSpan().AsEnumerable().First(value => value == 2));
            Assert.Throws<InvalidOperationException>(() =>
                source.AsSpan().AsEnumerable().First(value => value == 9)
            );
            Assert.True(source.AsSpan().AsEnumerable().TryFirst(value => value == 3, out int found));
            Assert.Equal(3, found);
            Assert.False(source.AsSpan().AsEnumerable().TryFirst(value => value == 9, out _));
        }

        [Fact]
        public static void SpanEnumerable_ToArray_ReturnsEmptyArrayWhenNothingMatches()
        {
            int[] result = new[] { 1, 3, 5 }
                .AsSpan()
                .AsEnumerable()
                .Where(_ => false)
                .ToArray();

            Assert.Empty(result);
        }
    }
}
