using System.Buffers.Binary;
using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>A big-endian byte writer for OpenType tables.</summary>
internal sealed class ByteWriter
{
    private readonly MemoryStream _stream = new(1024);

    /// <summary>The number of bytes written.</summary>
    public int Length => (int)_stream.Length;

    /// <summary>Writes one byte.</summary>
    public void U8(byte value) => _stream.WriteByte(value);

    /// <summary>Writes an unsigned 16-bit integer.</summary>
    public void U16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes a signed 16-bit integer.</summary>
    public void I16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes an unsigned 32-bit integer.</summary>
    public void U32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes a signed 32-bit integer.</summary>
    public void I32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes a fixed-point 16.16 value.</summary>
    public void Fixed(float value) => I32((int)MathF.Round(value * 65536f));

    /// <summary>Writes a four-character tag.</summary>
    public void Tag(string tag)
    {
        Span<byte> buffer = stackalloc byte[4];
        for (int i = 0; i < 4; i++)
        {
            buffer[i] = i < tag.Length ? (byte)tag[i] : (byte)' ';
        }

        _stream.Write(buffer);
    }

    /// <summary>Writes raw bytes.</summary>
    public void Bytes(ReadOnlySpan<byte> data) => _stream.Write(data);

    /// <summary>Pads with zeroes to a four-byte boundary.</summary>
    public void Align4()
    {
        while ((_stream.Length & 3) != 0)
        {
            _stream.WriteByte(0);
        }
    }

    /// <summary>Overwrites four bytes at an absolute offset.</summary>
    public void PatchU32(int offset, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        long position = _stream.Position;
        _stream.Position = offset;
        _stream.Write(buffer);
        _stream.Position = position;
    }

    /// <summary>The bytes written so far.</summary>
    public byte[] ToArray() => _stream.ToArray();
}

/// <summary>One sfnt table ready to be written.</summary>
internal sealed record SfntTable(string Tag, byte[] Data)
{
    /// <summary>The table checksum.</summary>
    public uint Checksum => FontBinary.Checksum(Data);
}

/// <summary>Shared binary helpers: checksums, padding and base-128 integers.</summary>
internal static class FontBinary
{
    /// <summary>The sum of big-endian 32-bit words, zero padded to a word boundary.</summary>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int full = data.Length & ~3;
        for (int i = 0; i < full; i += 4)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i, 4));
        }

        if (full < data.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            tail.Clear();
            data[full..].CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }

        return sum;
    }

    /// <summary>Pads a table to a four-byte boundary.</summary>
    public static byte[] Pad(byte[] data)
    {
        int padded = (data.Length + 3) & ~3;
        if (padded == data.Length)
        {
            return data;
        }

        byte[] copy = new byte[padded];
        data.CopyTo(copy, 0);
        return copy;
    }

    /// <summary>Writes an OpenType UIntBase128 value.</summary>
    public static void Base128(ByteWriter writer, int value)
    {
        if (value == 0)
        {
            writer.U8(0);
            return;
        }

        Span<byte> bytes = stackalloc byte[5];
        int index = 5;
        int remaining = value;
        while (remaining > 0)
        {
            bytes[--index] = (byte)(remaining & 0x7F);
            remaining >>= 7;
        }

        for (int i = index; i < 5; i++)
        {
            byte value8 = bytes[i];
            if (i < 4)
            {
                value8 |= 0x80;
            }

            writer.U8(value8);
        }
    }

    /// <summary>Encodes a CFF DICT integer operand.</summary>
    public static void DictInt(ByteWriter writer, int value)
    {
        writer.U8(29);
        writer.I32(value);
    }

    /// <summary>Encodes a Type 2 charstring number.</summary>
    public static void CharStringInt(ByteWriter writer, int value)
    {
        if (value >= -107 && value <= 107)
        {
            writer.U8((byte)(value + 139));
        }
        else if (value >= 108 && value <= 1131)
        {
            int v = value - 108;
            writer.U8((byte)((v >> 8) + 247));
            writer.U8((byte)(v & 0xFF));
        }
        else if (value >= -1131 && value <= -108)
        {
            int v = -value - 108;
            writer.U8((byte)((v >> 8) + 251));
            writer.U8((byte)(v & 0xFF));
        }
        else
        {
            writer.U8(28);
            writer.I16((short)Math.Clamp(value, short.MinValue, short.MaxValue));
        }
    }

    /// <summary>Converts a string into Mac Roman bytes, falling back to '?'.</summary>
    public static byte[] MacRoman(string text)
    {
        byte[] bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bytes[i] = c < 128 ? (byte)c : (byte)'?';
        }

        return bytes;
    }

    /// <summary>Converts a string into UTF-16 big-endian bytes.</summary>
    public static byte[] Utf16BigEndian(string text)
    {
        byte[] bytes = new byte[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(i * 2, 2), text[i]);
        }

        return bytes;
    }

    /// <summary>The number of seconds between 1904 and 1970, for OpenType dates.</summary>
    public static long OpenTypeDate(DateTime moment) =>
        (long)(moment.ToUniversalTime() - new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    /// <summary>A Windows-1252 byte for a code point, used by Mac name records.</summary>
    public static string Ascii(string text)
    {
        StringBuilder builder = new(text.Length);
        foreach (char c in text)
        {
            builder.Append(c < 128 ? c : '?');
        }

        return builder.ToString();
    }
}
