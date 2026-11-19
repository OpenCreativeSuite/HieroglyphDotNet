using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>Writes a bare CFF table so the same outlines can ship as OpenType.</summary>
internal static class CffTable
{
    private const int StandardStringCount = 391;

    /// <summary>Builds the table for an ordered glyph list where index zero is .notdef.</summary>
    public static byte[] Build(IReadOnlyList<Glyph> glyphs, string postScriptName, FontMetrics metrics)
    {
        List<byte[]> charStrings = [];
        List<string> names = [];

        foreach (Glyph glyph in glyphs)
        {
            charStrings.Add(CharString(glyph));
            names.Add(Sanitise(glyph.Name, glyph.Codepoint));
        }

        byte[] nameIndex = Index([Encoding.ASCII.GetBytes(FontBinary.Ascii(postScriptName))]);
        byte[] stringIndex = Index(names.Select(name => Encoding.ASCII.GetBytes(name)).ToList());
        byte[] globalSubr = [0, 0];

        int charsetSize = 1 + (Math.Max(1, glyphs.Count) - 1) * 2;
        byte[] charStringIndex = Index(charStrings);

        Bounds bounds = Bounds.Empty;
        foreach (Glyph glyph in glyphs)
        {
            if (glyph.HasOutline)
            {
                bounds = bounds.Union(glyph.OutlineBounds);
            }
        }

        if (bounds.IsEmpty)
        {
            bounds = new Bounds(0f, 0f, 0f, 0f);
        }

        byte[] probe = TopDict(0, 0, 0, 0, bounds);
        int topIndexSize = Index([probe]).Length;

        // Private DICT: default and nominal widths of zero, so every
        // charstring states its own advance first.
        ByteWriter priv = new();
        FontBinary.DictInt(priv, 0);
        priv.U8(20);
        FontBinary.DictInt(priv, 0);
        priv.U8(21);
        byte[] privateData = priv.ToArray();

        int charsetOffset = 4 + nameIndex.Length + topIndexSize + stringIndex.Length + globalSubr.Length;
        int charStringsOffset = charsetOffset + charsetSize;
        int privateSize = privateData.Length;
        int privateOffset = charStringsOffset + charStringIndex.Length;

        byte[] topDict = TopDict(charsetOffset, charStringsOffset, privateSize, privateOffset, bounds);

        ByteWriter writer = new();
        writer.U8(1);
        writer.U8(0);
        writer.U8(4);
        writer.U8(1);
        writer.Bytes(nameIndex);
        writer.Bytes(Index([topDict]));
        writer.Bytes(stringIndex);
        writer.Bytes(globalSubr);

        // Charset, format 0: one SID per glyph after .notdef.
        writer.U8(0);
        for (int i = 1; i < glyphs.Count; i++)
        {
            writer.U16((ushort)(StandardStringCount + i));
        }

        writer.Bytes(charStringIndex);
        writer.Bytes(privateData);

        return writer.ToArray();
    }

    private static byte[] TopDict(int charsetOffset, int charStringsOffset, int privateSize, int privateOffset, Bounds bounds)
    {
        ByteWriter dict = new();

        FontBinary.DictInt(dict, (int)bounds.MinX);
        FontBinary.DictInt(dict, (int)bounds.MinY);
        FontBinary.DictInt(dict, (int)bounds.MaxX);
        FontBinary.DictInt(dict, (int)bounds.MaxY);
        dict.U8(5);

        FontBinary.DictInt(dict, charsetOffset);
        dict.U8(15);

        FontBinary.DictInt(dict, charStringsOffset);
        dict.U8(17);

        FontBinary.DictInt(dict, privateSize);
        FontBinary.DictInt(dict, privateOffset);
        dict.U8(18);

        return dict.ToArray();
    }

    private static byte[] CharString(Glyph glyph)    {
        ByteWriter charString = new();
        FontBinary.CharStringInt(charString, Math.Max(0, glyph.AdvanceWidth));

        bool first = true;
        Vec2 origin = Vec2.Zero;
        foreach (GlyphContour contour in glyph.Contours)
        {
            if (contour.Nodes.Count < 2)
            {
                continue;
            }

            List<GlyphNode> nodes = contour.Nodes;
            Vec2 start = nodes[0].Point;
            EmitDelta(charString, start - origin);
            charString.U8(21);
            origin = start;
            first = false;

            int segments = contour.Closed ? nodes.Count : nodes.Count - 1;
            Vec2 current = start;
            for (int i = 0; i < segments; i++)
            {
                GlyphNode from = nodes[i];
                GlyphNode to = nodes[(i + 1) % nodes.Count];
                Vec2 end = to.Point;

                if (!from.HasOut && !to.HasIn)
                {
                    EmitDelta(charString, end - current);
                    charString.U8(5);
                }
                else
                {
                    Vec2 c1 = from.HasOut ? from.Out : from.Point;
                    Vec2 c2 = to.HasIn ? to.In : to.Point;
                    EmitDelta(charString, c1 - current);
                    EmitDelta(charString, c2 - c1);
                    EmitDelta(charString, end - c2);
                    charString.U8(8);
                }

                current = end;
            }

            // Type 2 has no closepath operator: the next rmoveto (or endchar)
            // closes the current subpath implicitly.
            origin = start;
        }

        if (first)
        {
            // A blank glyph still needs its width and an end marker.
            charString.U8(14);
            return charString.ToArray();
        }

        charString.U8(14);
        return charString.ToArray();
    }

    private static void EmitDelta(ByteWriter writer, Vec2 delta)
    {
        FontBinary.CharStringInt(writer, (int)MathF.Round(delta.X));
        FontBinary.CharStringInt(writer, (int)MathF.Round(delta.Y));
    }

    /// <summary>Builds a CFF INDEX structure.</summary>
    private static byte[] Index(IReadOnlyList<byte[]> items)
    {
        ByteWriter writer = new();
        writer.U16((ushort)items.Count);
        if (items.Count == 0)
        {
            return writer.ToArray();
        }

        int total = 0;
        foreach (byte[] item in items)
        {
            total += item.Length;
        }

        int offSize = total + 1 <= 0xFF ? 1 : total + 1 <= 0xFFFF ? 2 : total + 1 <= 0xFFFFFF ? 3 : 4;
        writer.U8((byte)offSize);

        int offset = 1;
        WriteOffset(writer, offset, offSize);
        foreach (byte[] item in items)
        {
            offset += item.Length;
            WriteOffset(writer, offset, offSize);
        }

        foreach (byte[] item in items)
        {
            writer.Bytes(item);
        }

        return writer.ToArray();
    }

    private static void WriteOffset(ByteWriter writer, int value, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        {
            writer.U8((byte)((value >> (i * 8)) & 0xFF));
        }
    }

    private static string Sanitise(string name, int codepoint)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "uni" + codepoint.ToString("X4");
        }

        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                builder.Append('_');
            }
        }

        string clean = builder.ToString();
        return clean.Length == 0 ? "uni" + codepoint.ToString("X4") : clean;
    }
}
