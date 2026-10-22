using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Utils
{
    public class SpanExtensionTests
    {
        [Fact]
        public static void SpanEnumerable_ToArray_CollectsFilteredItemsBeyondInitialBuffer()
        {
            int[] source = Enumerable.Range(0, 128).ToArray();

            int[] result = source.AsSpan()
                .AsValueEnumerable()
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
                .AsValueEnumerable()
                .Where(_ => false)
                .ToArray();

            Assert.Empty(result);
        }
    }
}
