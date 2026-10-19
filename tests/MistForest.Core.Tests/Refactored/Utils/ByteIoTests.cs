using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Utils
{
    public class ByteIoTests
    {
        [Fact]
        public static void Int_RoundTrip()
        {
            byte[] buffer = new byte[4];
            ByteWriter writer = ByteWriter.New(ref buffer);
            Assert.True(writer.WriteInt(-12345, out _));

            ByteReader reader = ByteReader.New(Box.New(buffer))!;
            Assert.Equal(-12345, reader.ReadInt(out _));
        }

        [Fact]
        public static void ULong_RoundTrip()
        {
            byte[] buffer = new byte[8];
            ByteWriter writer = ByteWriter.New(ref buffer);
            Assert.True(writer.WriteULong(0xDEADBEEFCAFEBABEul, out _));

            ByteReader reader = ByteReader.New(Box.New(buffer))!;
            Assert.Equal(0xDEADBEEFCAFEBABEul, reader.ReadULong(out _));
        }

        [Fact]
        public static void Byte_RoundTrip()
        {
            byte[]? buffer = new byte[1];
            ByteWriter writer = ByteWriter.New(ref buffer);
            Assert.True(writer.WriteByte(0xAB, out _));

            ByteReader reader = ByteReader.New(Box.New(buffer))!;
            Assert.Equal((byte)0xAB, reader.ReadByte(out _));
        }

        [Fact]
        public static void Sequential_MixedTypes()
        {
            byte[]? buffer = new byte[15];
            ByteWriter writer = ByteWriter.New(ref buffer);
            writer.WriteInt(42, out _);
            writer.WriteULong(999, out _);
            writer.WriteByte(1, out _);
            writer.WriteByte(2, out _);
            writer.WriteByte(3, out _);

            ByteReader reader = ByteReader.New(Box.New(buffer))!;
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
            Assert.Null(ByteReader.New(Box.New(new byte[3]))!.ReadInt(out string? e1));
            Assert.NotNull(e1);

            Assert.Null(ByteReader.New(Box.New(new byte[7]))!.ReadULong(out string? e2));
            Assert.NotNull(e2);

            Assert.Null(ByteReader.New(Box.New(Array.Empty<byte>()))!.ReadByte(out string? e3));
            Assert.NotNull(e3);
        }

        [Fact]
        public static void Writer_UndersizedBuffer()
        {
            var buf = new byte[4];
            ByteWriter writer = ByteWriter.New(ref buf);
            Assert.False(writer.WriteULong(1, out string? error));
            Assert.NotNull(error);

            buf = new byte[1];
            ByteWriter writer2 = ByteWriter.New(ref buf);
            Assert.False(writer2.WriteInt(1, out _));
        }

        [Fact]
        public static void LittleEndian_ByteOrder()
        {
            byte[] buffer = new byte[4];
            ByteWriter.New(ref buffer).WriteInt(1, out _);
            Assert.Equal(0x01, buffer[0]);
            Assert.Equal(0x00, buffer[1]);
            Assert.Equal(0x00, buffer[2]);
            Assert.Equal(0x00, buffer[3]);

            ByteWriter.New(ref buffer).WriteInt(0x01020304, out _);
            Assert.Equal(0x04, buffer[0]);
            Assert.Equal(0x03, buffer[1]);
            Assert.Equal(0x02, buffer[2]);
            Assert.Equal(0x01, buffer[3]);
        }
    }
}
