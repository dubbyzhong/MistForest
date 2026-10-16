using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored
{
    public class BitsetTests
    {
        [Fact]
        public static void Capacity_AndWordCount()
        {
            Bitset bitset = Bitset.WithCapacity(25);
            Assert.Equal(25, bitset.Capacity);
            Assert.Equal(4, bitset.Words.Length);
            Assert.Single(Bitset.WithCapacity(8).Words);
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

            Assert.True(bitset.SetInner(3, ulong.MaxValue, out _));
            Assert.Equal(ulong.MaxValue, bitset.Words[3]);

            Assert.False(bitset.SetInner(4, 0, out _));
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
    }

    public class ByteIoTests
    {
        [Fact]
        public static void Int_RoundTrip()
        {
            byte[] buffer = new byte[4];
            ByteWriter writer = ByteWriter.New(buffer);
            Assert.True(writer.WriteInt(-12345, out _));

            ByteReader reader = ByteReader.New(buffer);
            Assert.Equal(-12345, reader.ReadInt(out _));
        }

        [Fact]
        public static void ULong_RoundTrip()
        {
            byte[] buffer = new byte[8];
            ByteWriter writer = ByteWriter.New(buffer);
            Assert.True(writer.WriteULong(0xDEADBEEFCAFEBABEul, out _));

            ByteReader reader = ByteReader.New(buffer);
            Assert.Equal(0xDEADBEEFCAFEBABEul, reader.ReadULong(out _));
        }

        [Fact]
        public static void Byte_RoundTrip()
        {
            byte[] buffer = new byte[1];
            ByteWriter writer = ByteWriter.New(buffer);
            Assert.True(writer.WriteByte(0xAB, out _));

            ByteReader reader = ByteReader.New(buffer);
            Assert.Equal((byte)0xAB, reader.ReadByte(out _));
        }

        [Fact]
        public static void Sequential_MixedTypes()
        {
            byte[] buffer = new byte[15];
            ByteWriter writer = ByteWriter.New(buffer);
            writer.WriteInt(42, out _);
            writer.WriteULong(999, out _);
            writer.WriteByte(1, out _);
            writer.WriteByte(2, out _);
            writer.WriteByte(3, out _);

            ByteReader reader = ByteReader.New(buffer);
            Assert.Equal(42, reader.ReadInt(out _));
            Assert.Equal(999ul, reader.ReadULong(out _));
            Assert.Equal((byte)1, reader.ReadByte(out _));
            Assert.Equal((byte)2, reader.ReadByte(out _));
            Assert.Equal((byte)3, reader.ReadByte(out _));
            Assert.Null(reader.ReadByte(out string? error));
            Assert.NotNull(error);
        }

        [Fact]
        public static void Reader_InsufficientBytes()
        {
            Assert.Null(ByteReader.New(new byte[3]).ReadInt(out string? e1));
            Assert.NotNull(e1);
            
            Assert.Null(ByteReader.New(new byte[7]).ReadULong(out string? e2));
            Assert.NotNull(e2);

            Assert.Null(ByteReader.New(Array.Empty<byte>()).ReadByte(out string? e3));
            Assert.NotNull(e3);
        }

        [Fact]
        public static void Writer_UndersizedBuffer()
        {
            ByteWriter writer = ByteWriter.New(new byte[4]);
            Assert.False(writer.WriteULong(1, out string? error));
            Assert.NotNull(error);
            
            ByteWriter writer2 = ByteWriter.New(new byte[1]);
            Assert.False(writer2.WriteInt(1, out _));
        }

        [Fact]
        public static void LittleEndian_ByteOrder()
        {
            byte[] buffer = new byte[4];
            ByteWriter.New(buffer).WriteInt(1, out _);
            Assert.Equal(0x01, buffer[0]);
            Assert.Equal(0x00, buffer[1]);
            Assert.Equal(0x00, buffer[2]);
            Assert.Equal(0x00, buffer[3]);

            ByteWriter.New(buffer).WriteInt(0x01020304, out _);
            Assert.Equal(0x04, buffer[0]);
            Assert.Equal(0x03, buffer[1]);
            Assert.Equal(0x02, buffer[2]);
            Assert.Equal(0x01, buffer[3]);
        }
    }
}
