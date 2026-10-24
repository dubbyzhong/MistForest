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
