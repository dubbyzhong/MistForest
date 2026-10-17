using System;
using System.Buffers.Binary;

namespace MistForest.Core.Utils
{
    public sealed class ByteReader
    {
        private readonly byte[] _bytes;
        private int _offset;

        private ByteReader(byte[] bytes) => _bytes = bytes;

        public static ByteReader? New(Box<byte[]> bytes)
        {
            if (bytes.TryTake() is not byte[] bytesInner)
            {
                return null;
            }
            ByteReader reader = new(bytesInner) { _offset = 0 };
            return reader;
        }

        public int? ReadInt(out string? error)
        {
            error = null;
            if (_offset + sizeof(int) > _bytes.Length)
            {
                error = "Not enough bytes to read an int.";
                return null;
            }
            int value = BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(_offset, sizeof(int)));
            _offset += sizeof(int);
            return value;
        }

        public ulong? ReadULong(out string? error)
        {
            error = null;
            if (_offset + sizeof(ulong) > _bytes.Length)
            {
                error = "Not enough bytes to read a ulong long.";
                return null;
            }
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(
                _bytes.AsSpan(_offset, sizeof(ulong))
            );
            _offset += sizeof(ulong);
            return value;
        }

        public byte? ReadByte(out string? error)
        {
            error = null;
            if (_offset >= _bytes.Length)
            {
                error = "Not enough bytes to read a byte.";
                return null;
            }
            byte value = _bytes[_offset];
            ++_offset;
            return value;
        }
    }

    public sealed class ByteWriter
    {
        private readonly byte[] _bytes;
        private int _offset;

        private ByteWriter(byte[] bytes) => _bytes = bytes;

        public static ByteWriter New(ref byte[] bytes)
        {
            return new(bytes) { _offset = 0 };
        }

        public bool WriteInt(int value, out string? error)
        {
            error = null;
            if (_offset + sizeof(int) > _bytes.Length)
            {
                error = "Not enough bytes to write an int.";
                return false;
            }
            BinaryPrimitives.WriteInt32LittleEndian(_bytes.AsSpan(_offset, sizeof(int)), value);
            _offset += sizeof(int);
            return true;
        }

        public bool WriteULong(ulong value, out string? error)
        {
            error = null;
            if (_offset + sizeof(ulong) > _bytes.Length)
            {
                error = "Not enough bytes to write a ulong long.";
                return false;
            }
            BinaryPrimitives.WriteUInt64LittleEndian(_bytes.AsSpan(_offset, sizeof(ulong)), value);
            _offset += sizeof(ulong);
            return true;
        }

        public bool WriteByte(byte value, out string? error)
        {
            error = null;
            if (_offset >= _bytes.Length)
            {
                error = "Not enough bytes to write a byte.";
                return false;
            }
            _bytes[_offset] = value;
            ++_offset;
            return true;
        }
    }
}
