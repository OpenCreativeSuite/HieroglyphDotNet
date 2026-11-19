using System.IO.Compression;

namespace HieroglyphDotNet.Core;

/// <summary>Assembles tables into an sfnt container and its web wrappers.</summary>
internal static class SfntBuilder
{
    private const uint MagicAdjustment = 0xB1B0AFBA;

    /// <summary>Builds a TTF or OTF container.</summary>
    public static byte[] Build(uint flavor, IReadOnlyList<SfntTable> tables)
    {
        List<SfntTable> sorted = [.. tables.OrderBy(table => table.Tag, StringComparer.Ordinal)];
        int count = sorted.Count;
        int power = 1;
        int selector = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            selector++;
        }

        int searchRange = power * 16;
        int rangeShift = (count * 16) - searchRange;

        int offset = (12 + (count * 16) + 3) & ~3;
        List<(SfntTable Table, int Offset, byte[] Padded)> placed = new(count);
        foreach (SfntTable table in sorted)
        {
            byte[] padded = FontBinary.Pad(table.Data);
            placed.Add((table, offset, padded));
            offset += padded.Length;
        }

        byte[] output = new byte[offset];
        Span<byte> header = output.AsSpan(0, 12);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, flavor);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(header[4..], (ushort)count);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(header[6..], (ushort)searchRange);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(header[8..], (ushort)selector);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(header[10..], (ushort)rangeShift);

        int record = 12;
        foreach ((SfntTable table, int tableOffset, byte[] padded) in placed)
        {
            Span<byte> entry = output.AsSpan(record, 16);
            for (int i = 0; i < 4; i++)
            {
                entry[i] = i < table.Tag.Length ? (byte)table.Tag[i] : (byte)' ';
            }

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(entry[4..], table.Checksum);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(entry[8..], (uint)tableOffset);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(entry[12..], (uint)table.Data.Length);
            padded.CopyTo(output, tableOffset);
            record += 16;
        }

        uint adjustment = MagicAdjustment - FontBinary.Checksum(output);
        foreach ((SfntTable table, int tableOffset, _) in placed)
        {
            if (table.Tag == "head")
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(tableOffset + 8, 4), adjustment);
                break;
            }
        }

        return output;
    }

    /// <summary>Builds a WOFF 1.0 container around the same tables.</summary>
    public static byte[] BuildWoff(uint flavor, IReadOnlyList<SfntTable> tables)
    {
        List<SfntTable> sorted = [.. tables.OrderBy(table => table.Tag, StringComparer.Ordinal)];
        List<(SfntTable Table, byte[] Stored)> stored = [];
        int totalSfntSize = 12 + (sorted.Count * 16);

        foreach (SfntTable table in sorted)
        {
            byte[] compressed = Deflate(table.Data);
            byte[] chosen = compressed.Length < table.Data.Length ? compressed : table.Data;
            stored.Add((table, chosen));
            totalSfntSize += FontBinary.Pad(table.Data).Length;
        }

        ByteWriter writer = new();
        writer.Tag("wOFF");
        writer.U32(flavor);
        writer.U32((uint)(44 + (stored.Count * 20) + stored.Sum(item => FontBinary.Pad(item.Stored).Length)));
        writer.U16((ushort)stored.Count);
        writer.U16(0);
        writer.U32((uint)totalSfntSize);
        writer.U16(1);
        writer.U16(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);

        int offset = 44 + (stored.Count * 20);
        foreach ((SfntTable table, byte[] data) in stored)
        {
            writer.Tag(table.Tag);
            writer.U32((uint)offset);
            writer.U32((uint)data.Length);
            writer.U32((uint)table.Data.Length);
            writer.U32(table.Checksum);
            offset += FontBinary.Pad(data).Length;
        }

        foreach ((_, byte[] data) in stored)
        {
            writer.Bytes(data);
            while ((writer.Length & 3) != 0)
            {
                writer.U8(0);
            }
        }

        return writer.ToArray();
    }

    /// <summary>Builds a WOFF 2.0 container using Brotli with null transforms.</summary>
    public static byte[] BuildWoff2(uint flavor, IReadOnlyList<SfntTable> tables)
    {
        List<SfntTable> sorted = [.. tables.OrderBy(table => table.Tag, StringComparer.Ordinal)];
        int totalSfntSize = 12 + (sorted.Count * 16) + sorted.Sum(table => FontBinary.Pad(table.Data).Length);

        ByteWriter directory = new();
        ByteWriter payload = new();

        foreach (SfntTable table in sorted)
        {
            int known = Array.IndexOf(KnownTags, table.Tag);
            int transform = table.Tag is "glyf" or "loca" ? 3 : 0;
            directory.U8((byte)((known < 0 ? 63 : known) | (transform << 6)));
            if (known < 0)
            {
                directory.Tag(table.Tag);
            }

            FontBinary.Base128(directory, table.Data.Length);
            payload.Bytes(table.Data);
        }

        byte[] raw = payload.ToArray();
        byte[] compressed = Brotli(raw);
        int unpadded = 48 + directory.Length + compressed.Length;
        int fileLength = (unpadded + 3) & ~3;

        ByteWriter writer = new();
        writer.Tag("wOF2");
        writer.U32(flavor);
        writer.U32((uint)fileLength);
        writer.U16((ushort)sorted.Count);
        writer.U16(0);
        writer.U32((uint)totalSfntSize);
        writer.U32((uint)compressed.Length);
        writer.U16(1);
        writer.U16(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.Bytes(directory.ToArray());
        writer.Bytes(compressed);

        // WOFF2 files are padded to a four-byte boundary and the header length
        // includes that padding; sanitizers reject files that stop short.
        while (writer.Length < fileLength)
        {
            writer.U8(0);
        }

        return writer.ToArray();
    }

    private static readonly string[] KnownTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post",
        "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG", "EBDT",
        "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
        "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH",
        "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar",
        "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
        "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop",
        "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill",
    ];

    private static byte[] Deflate(byte[] data)
    {
        using MemoryStream output = new();
        using (ZLibStream stream = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Brotli(byte[] data)
    {
        byte[] buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        if (!BrotliEncoder.TryCompress(data, buffer, out int written))
        {
            throw new InvalidOperationException("Brotli compression failed.");
        }

        return buffer.AsSpan(0, written).ToArray();
    }
}
