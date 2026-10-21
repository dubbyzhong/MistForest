using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Utils
{
    public class BitsetTests
    {
        [Fact]
        public static void Capacity_AndWordCount()
        {
            Bitset bitset = Bitset.WithCapacity(25);
            Assert.Equal(25, bitset.Capacity);
            Assert.Single(bitset.Words);
            Assert.Single(Bitset.WithCapacity(8).Words);
            Assert.Single(Bitset.WithCapacity(64).Words);
            Assert.Equal(2, Bitset.WithCapacity(65).Words.Length);
            Assert.Single(Bitset.WithCapacity(1).Words);
        }

        [Fact]
        public static void GetSet_RoundTrip()
        {
            Bitset bitset = Bitset.WithCapacity(70);
            for (int i = 0; i < 70; i += 2)
            {
                bitset[i] = true;
            }
            for (int i = 0; i < 70; ++i)
            {
                Assert.Equal(i % 2 == 0, bitset[i]);
            }
            bitset[10] = false;
            Assert.False(bitset[10]);
        }

        [Fact]
        public static void Indexer_OutOfRange_Throws()
        {
            Bitset bitset = Bitset.WithCapacity(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => bitset[10] = true);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = bitset[10]);
            Assert.Throws<ArgumentOutOfRangeException>(() => bitset[-1] = true);
        }

        [Fact]
        public static void SetInner_Valid_And_Invalid()
        {
            Bitset bitset = Bitset.WithCapacity(25);
            Assert.True(bitset.SetInner(0, 0xFF, out _));
            Assert.Equal((ulong)0xFF, bitset.Words[0]);

            Assert.False(bitset.SetInner(1, 0, out string? error));
            Assert.Equal("Index out of range", error);

            Bitset bigger = Bitset.WithCapacity(65);
            Assert.True(bigger.SetInner(1, ulong.MaxValue, out _));
            Assert.Equal(ulong.MaxValue, bigger.Words[1]);
        }

        [Fact]
        public static void Default_AllZero()
        {
            Bitset bitset = Bitset.WithCapacity(64);
            foreach (ulong word in bitset.Words)
            {
                Assert.Equal(0ul, word);
            }
            for (int i = 0; i < 64; ++i)
            {
                Assert.False(bitset[i]);
            }
        }

        [Fact]
        public static void SetInner_NegativeIndex_Fails()
        {
            Bitset bitset = Bitset.WithCapacity(25);
            Assert.False(bitset.SetInner(-1, 0xFF, out string? error));
            Assert.NotNull(error);
        }

        [Fact]
        public static void Clone_IsIndependent()
        {
            Bitset bitset = Bitset.WithCapacity(20);
            bitset[3] = true;
            Bitset clone = bitset.Clone();
            clone[5] = true;
            clone[3] = false;
            Assert.True(bitset[3]);
            Assert.False(bitset[5]);
            Assert.True(clone[5]);
            Assert.False(clone[3]);
        }
    }
}
