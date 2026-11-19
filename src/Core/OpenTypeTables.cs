using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>Builds the non-outline OpenType tables shared by TTF and OTF.</summary>
internal static class OpenTypeTables
{
    private readonly record struct Mapping(int Code, int Glyph);

    /// <summary>Builds a cmap with BMP and full-Unicode subtables.</summary>
    public static byte[] Cmap(IReadOnlyList<(int Code, int Glyph)> mappings)
    {
        List<Mapping> sorted = mappings
            .Where(pair => pair.Glyph > 0 && pair.Code >= 0 && pair.Code <= 0x10FFFF)
            .Select(pair => new Mapping(pair.Code, pair.Glyph))
            .OrderBy(mapping => mapping.Code)
            .ToList();

        byte[] format4 = CmapFormat4(sorted);
        byte[] format12 = CmapFormat12(sorted);

        ByteWriter writer = new();
        writer.U16(0);
        writer.U16(4);

        int recordStart = 4 + (4 * 8);
        int format4Offset = recordStart;
        int format12Offset = recordStart + format4.Length;

        writer.U16(0);
        writer.U16(3);
        writer.U32((uint)format4Offset);

        writer.U16(3);
        writer.U16(1);
        writer.U32((uint)format4Offset);

        writer.U16(0);
        writer.U16(4);
        writer.U32((uint)format12Offset);

        writer.U16(3);
        writer.U16(10);
        writer.U32((uint)format12Offset);

        writer.Bytes(format4);
        writer.Bytes(format12);
        return writer.ToArray();
    }

    private static byte[] CmapFormat4(List<Mapping> mappings)
    {
        List<Mapping> bmp = mappings.Where(mapping => mapping.Code <= 0xFFFE).ToList();
        int segments = bmp.Count + 1;

        int power = 1;
        int selector = 0;
        while (power * 2 <= segments)
        {
            power *= 2;
            selector++;
        }

        ByteWriter writer = new();
        writer.U16(4);
        int lengthPosition = writer.Length;
        writer.U16(0);
        writer.U16(0);
        writer.U16((ushort)(segments * 2));
        writer.U16((ushort)(power * 2));
        writer.U16((ushort)selector);
        writer.U16((ushort)((segments * 2) - (power * 2)));

        foreach (Mapping mapping in bmp)
        {
            writer.U16((ushort)mapping.Code);
        }

        writer.U16(0xFFFF);
        writer.U16(0);

        foreach (Mapping mapping in bmp)
        {
            writer.U16((ushort)mapping.Code);
        }

        writer.U16(0xFFFF);

        foreach (Mapping mapping in bmp)
        {
            writer.U16((ushort)((mapping.Glyph - mapping.Code) & 0xFFFF));
        }

        writer.U16(1);

        for (int i = 0; i < segments; i++)
        {
            writer.U16(0);
        }

        byte[] data = writer.ToArray();
        data[lengthPosition] = (byte)(data.Length >> 8);
        data[lengthPosition + 1] = (byte)(data.Length & 0xFF);
        return data;
    }

    private static byte[] CmapFormat12(List<Mapping> mappings)
    {
        List<(int Start, int End, int Glyph)> groups = [];
        foreach (Mapping mapping in mappings)
        {
            if (groups.Count > 0)
            {
                (int start, int end, int glyph) = groups[^1];
                if (mapping.Code == end + 1 && mapping.Glyph == glyph + (end - start) + 1)
                {
                    groups[^1] = (start, mapping.Code, glyph);
                    continue;
                }
            }

            groups.Add((mapping.Code, mapping.Code, mapping.Glyph));
        }

        ByteWriter writer = new();
        writer.U16(12);
        writer.U16(0);
        writer.U32((uint)(16 + (groups.Count * 12)));
        writer.U32(0);
        writer.U32((uint)groups.Count);

        foreach ((int start, int end, int glyph) in groups)
        {
            writer.U32((uint)start);
            writer.U32((uint)end);
            writer.U32((uint)glyph);
        }

        return writer.ToArray();
    }

    /// <summary>Builds the name table.</summary>
    public static byte[] Name(FontProject project, FontStyle style, string postScriptName)
    {
        string family = string.IsNullOrWhiteSpace(project.FamilyName) ? "Untitled" : project.FamilyName.Trim();
        string subfamily = style.Name;
        string full = $"{family} {subfamily}".Trim();
        string version = "Version " + NormaliseVersion(project.Version);
        string unique = $"{family};{project.Version};{subfamily}";

        List<(int Id, string Value)> entries =
        [
            (0, project.Copyright),
            (1, family),
            (2, subfamily),
            (3, unique),
            (4, full),
            (5, version),
            (6, postScriptName),
        ];

        if (!string.IsNullOrWhiteSpace(project.Designer))
        {
            entries.Add((9, project.Designer));
        }

        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            entries.Add((10, project.Description));
        }

        if (!string.IsNullOrWhiteSpace(project.License))
        {
            entries.Add((13, project.License));
        }

        List<(int Platform, int Encoding, int Language, int Id, byte[] Data)> records = [];

        foreach ((int id, string value) in entries)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            records.Add((3, 1, 0x0409, id, FontBinary.Utf16BigEndian(value)));
        }

        foreach ((int id, string value) in entries)
        {
            if (string.IsNullOrEmpty(value) || id is not (1 or 2 or 4 or 6))
            {
                continue;
            }

            records.Add((1, 0, 0, id, FontBinary.MacRoman(FontBinary.Ascii(value))));
        }

        records.Sort(static (left, right) =>
        {
            int order = left.Platform.CompareTo(right.Platform);
            if (order != 0)
            {
                return order;
            }

            order = left.Encoding.CompareTo(right.Encoding);
            if (order != 0)
            {
                return order;
            }

            order = left.Language.CompareTo(right.Language);
            return order != 0 ? order : left.Id.CompareTo(right.Id);
        });

        ByteWriter writer = new();
        writer.U16(0);
        writer.U16((ushort)records.Count);
        writer.U16((ushort)(6 + (records.Count * 12)));

        int offset = 0;
        foreach ((int platform, int encoding, int language, int id, byte[] data) in records)
        {
            writer.U16((ushort)platform);
            writer.U16((ushort)encoding);
            writer.U16((ushort)language);
            writer.U16((ushort)id);
            writer.U16((ushort)data.Length);
            writer.U16((ushort)offset);
            offset += data.Length;
        }

        foreach ((_, _, _, _, byte[] data) in records)
        {
            writer.Bytes(data);
        }

        return writer.ToArray();
    }

    /// <summary>Builds the OS/2 table at version 4.</summary>
    public static byte[] Os2(
        FontStyle style,
        IReadOnlyList<int> advances,
        IReadOnlyList<int> codes,
        bool hasKerning,
        short yMax,
        short yMin)
    {
        FontMetrics metrics = style.Metrics;
        int average = advances.Count > 0 ? (int)advances.Average() : 600;

        bool bold = style.Weight >= 700;
        int selection = (style.Italic ? 1 : 0) | (bold ? 0x20 : 0);
        if (selection == 0)
        {
            selection = 0x40;
        }

        bool latin1 = codes.Any(code => code is >= 0x80 and <= 0xFF);
        bool punctuation = codes.Any(code => code is >= 0x2000 and <= 0x206F);
        uint unicode1 = 1u | (latin1 ? 0x2u : 0u);

        ushort first = (ushort)Math.Clamp(codes.Where(code => code <= 0xFFFF).DefaultIfEmpty(0x20).Min(), 0, 0xFFFF);
        ushort last = (ushort)Math.Clamp(codes.Where(code => code <= 0xFFFF).DefaultIfEmpty(0x20).Max(), 0, 0xFFFF);

        ByteWriter writer = new();
        writer.U16(4);
        writer.I16((short)Math.Clamp(average, short.MinValue, short.MaxValue));
        writer.U16((ushort)Math.Clamp(style.Weight, 1, 1000));
        writer.U16((ushort)Math.Clamp(style.WidthClass, 1, 9));
        writer.U16(0);
        writer.I16(650);
        writer.I16(600);
        writer.I16(0);
        writer.I16(75);
        writer.I16(650);
        writer.I16(600);
        writer.I16(0);
        writer.I16(350);
        writer.I16(50);
        writer.I16(250);
        writer.I16(0);

        for (int i = 0; i < 10; i++)
        {
            writer.U8(0);
        }

        writer.U32(unicode1);
        writer.U32(punctuation ? 0x80000000u : 0u);
        writer.U32(0);
        writer.U32(0);
        writer.Tag("HGLF");
        writer.U16((ushort)selection);
        writer.U16(first);
        writer.U16(last);
        writer.I16((short)Math.Clamp(metrics.Ascender, short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(metrics.Descender, short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(metrics.LineGap, 0, short.MaxValue));
        writer.U16((ushort)Math.Clamp(Math.Max(metrics.Ascender, yMax), 0, ushort.MaxValue));
        writer.U16((ushort)Math.Clamp(Math.Max(-metrics.Descender, -yMin), 0, ushort.MaxValue));
        writer.U32(1);
        writer.U32(0);
        writer.I16((short)Math.Clamp(metrics.XHeight, 0, short.MaxValue));
        writer.I16((short)Math.Clamp(metrics.CapHeight, 0, short.MaxValue));
        writer.U16(0);
        writer.U16(0x20);
        writer.U16(hasKerning ? (ushort)2 : (ushort)1);
        return writer.ToArray();
    }

    /// <summary>Builds the head table.</summary>
    public static byte[] Head(
        FontProject project,
        FontMetrics metrics,
        short xMin,
        short yMin,
        short xMax,
        short yMax,
        int macStyle,
        int indexToLocFormat)
    {
        ByteWriter writer = new();
        writer.U32(0x00010000);
        writer.Fixed(ParseVersion(project.Version));
        writer.U32(0);
        writer.U32(0x5F0F3CF5);
        writer.U16(0x000B);
        writer.U16((ushort)metrics.UnitsPerEm);

        long created = FontBinary.OpenTypeDate(DateTime.UtcNow);
        uint low = (uint)Math.Clamp(created, 0, uint.MaxValue);
        writer.U32(0);
        writer.U32(low);
        writer.U32(0);
        writer.U32(low);

        writer.I16(xMin);
        writer.I16(yMin);
        writer.I16(xMax);
        writer.I16(yMax);
        writer.U16((ushort)macStyle);
        writer.U16(8);
        writer.I16(2);
        writer.I16((short)indexToLocFormat);
        writer.I16(0);
        return writer.ToArray();
    }

    /// <summary>Builds the hhea table.</summary>
    public static byte[] Hhea(
        FontStyle style,
        IReadOnlyList<int> advances,
        IReadOnlyList<int> leftBearings,
        IReadOnlyList<int> rightBearings,
        IReadOnlyList<int> extents,
        float italicAngle)
    {
        FontMetrics metrics = style.Metrics;
        ByteWriter writer = new();
        writer.U32(0x00010000);
        writer.I16((short)Math.Clamp(metrics.Ascender, short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(metrics.Descender, short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(metrics.LineGap, 0, short.MaxValue));
        writer.U16((ushort)Math.Clamp(advances.DefaultIfEmpty(0).Max(), 0, ushort.MaxValue));
        writer.I16((short)Math.Clamp(leftBearings.DefaultIfEmpty(0).Min(), short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(rightBearings.DefaultIfEmpty(0).Min(), short.MinValue, short.MaxValue));
        writer.I16((short)Math.Clamp(extents.DefaultIfEmpty(0).Max(), short.MinValue, short.MaxValue));
        writer.I16(1);
        writer.I16((short)Math.Clamp((int)MathF.Round(MathF.Tan(italicAngle * MathF.PI / 180f) * 1000f), short.MinValue, short.MaxValue));
        writer.I16(0);
        writer.I16(0);
        writer.I16(0);
        writer.I16(0);
        writer.I16(0);
        writer.I16(0);
        writer.U16((ushort)Math.Max(1, advances.Count));
        return writer.ToArray();
    }

    /// <summary>Builds maxp for TrueType outlines.</summary>
    public static byte[] Maxp(int glyphCount, int maxPoints, int maxContours)
    {
        ByteWriter writer = new();
        writer.U32(0x00010000);
        writer.U16((ushort)glyphCount);
        writer.U16((ushort)maxPoints);
        writer.U16((ushort)maxContours);
        writer.U16(0);
        writer.U16(0);
        writer.U16(2);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        writer.U16(0);
        return writer.ToArray();
    }

    /// <summary>Builds maxp for CFF outlines.</summary>
    public static byte[] MaxpCff(int glyphCount)
    {
        ByteWriter writer = new();
        writer.U32(0x00005000);
        writer.U16((ushort)glyphCount);
        return writer.ToArray();
    }

    /// <summary>Builds the post table.</summary>
    public static byte[] Post(bool italic, float italicAngle)
    {
        ByteWriter writer = new();
        writer.U32(0x00030000);
        writer.Fixed(italicAngle);
        writer.I16(-100);
        writer.I16(50);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        return writer.ToArray();
    }

    /// <summary>Builds a legacy kern table for pair adjustments.</summary>
    public static byte[] Korn(IReadOnlyList<(int Left, int Right, int Value)> pairs)
    {
        List<(int Left, int Right, int Value)> sorted = pairs
            .Where(pair => pair.Value != 0)
            .OrderBy(pair => pair.Left)
            .ThenBy(pair => pair.Right)
            .ToList();

        int power = 1;
        int selector = 0;
        while (power * 2 <= sorted.Count)
        {
            power *= 2;
            selector++;
        }

        ByteWriter writer = new();
        writer.U16(0);
        writer.U16(sorted.Count == 0 ? (ushort)0 : (ushort)1);
        if (sorted.Count == 0)
        {
            return writer.ToArray();
        }

        int subtableLength = 14 + (sorted.Count * 6);
        writer.U16(0);
        writer.U16((ushort)subtableLength);
        writer.U16(0x0001);
        writer.U16((ushort)sorted.Count);
        writer.U16((ushort)(power * 6));
        writer.U16((ushort)selector);
        writer.U16((ushort)((sorted.Count * 6) - (power * 6)));

        foreach ((int left, int right, int value) in sorted)
        {
            writer.U16((ushort)left);
            writer.U16((ushort)right);
            writer.I16((short)Math.Clamp(value, short.MinValue, short.MaxValue));
        }

        return writer.ToArray();
    }

    /// <summary>Builds a GPOS table with one pair-positioning lookup.</summary>
    public static byte[] Gpos(IReadOnlyList<(int Left, int Right, int Value)> pairs)
    {
        Dictionary<int, List<(int Right, int Value)>> byLeft = [];
        foreach ((int left, int right, int value) in pairs)
        {
            if (value == 0)
            {
                continue;
            }

            if (!byLeft.TryGetValue(left, out List<(int Right, int Value)>? list))
            {
                list = [];
                byLeft[left] = list;
            }

            list.Add((right, value));
        }

        List<int> firstGlyphs = byLeft.Keys.OrderBy(id => id).ToList();
        foreach (int key in firstGlyphs)
        {
            byLeft[key].Sort((a, b) => a.Right.CompareTo(b.Right));
        }

        List<byte[]> pairSets = [];
        foreach (int key in firstGlyphs)
        {
            ByteWriter set = new();
            List<(int Right, int Value)> records = byLeft[key];
            set.U16((ushort)records.Count);
            foreach ((int right, int value) in records)
            {
                set.U16((ushort)right);
                set.I16((short)Math.Clamp(value, short.MinValue, short.MaxValue));
            }

            pairSets.Add(set.ToArray());
        }

        ByteWriter coverage = new();
        coverage.U16(1);
        coverage.U16((ushort)firstGlyphs.Count);
        foreach (int glyph in firstGlyphs)
        {
            coverage.U16((ushort)glyph);
        }

        byte[] coverageData = coverage.ToArray();

        int header = 10 + (firstGlyphs.Count * 2);
        List<int> pairOffsets = [];
        int cursor = header;
        foreach (byte[] set in pairSets)
        {
            pairOffsets.Add(cursor);
            cursor += set.Length;
        }

        int coverageOffset = cursor;

        ByteWriter sub = new();
        sub.U16(1);
        sub.U16((ushort)coverageOffset);
        sub.U16(0x0004);
        sub.U16(0);
        sub.U16((ushort)firstGlyphs.Count);
        foreach (int offset in pairOffsets)
        {
            sub.U16((ushort)offset);
        }

        foreach (byte[] set in pairSets)
        {
            sub.Bytes(set);
        }

        sub.Bytes(coverageData);

        byte[] subtable = sub.ToArray();

        // Header (10) + ScriptList + FeatureList + LookupList.
        ByteWriter script = new();
        script.U16(1);
        script.Tag("DFLT");
        script.U16(8);
        script.U16(4);
        script.U16(0);
        script.U16(0);
        script.U16(0xFFFF);
        script.U16(1);
        script.U16(0);

        byte[] scriptData = script.ToArray();

        ByteWriter feature = new();
        feature.U16(1);
        feature.Tag("kern");
        feature.U16(8);
        feature.U16(0);
        feature.U16(1);
        feature.U16(0);

        byte[] featureData = feature.ToArray();

        int scriptOffset = 10;
        int featureOffset = scriptOffset + scriptData.Length;
        int lookupOffset = featureOffset + featureData.Length;

        int lookupHeader = 8;
        int subtableOffset = lookupHeader;

        ByteWriter lookup = new();
        lookup.U16(2);
        lookup.U16(0);
        lookup.U16(1);
        lookup.U16((ushort)subtableOffset);
        lookup.Bytes(subtable);

        byte[] lookupData = lookup.ToArray();

        ByteWriter writer = new();
        writer.U32(0x00010000);
        writer.U16((ushort)scriptOffset);
        writer.U16((ushort)featureOffset);
        writer.U16((ushort)lookupOffset);
        writer.Bytes(scriptData);
        writer.Bytes(featureData);
        writer.U16(1);
        writer.U16(4);
        writer.Bytes(lookupData);
        return writer.ToArray();
    }

    private static string NormaliseVersion(string version)
    {
        string clean = version.Trim();
        if (clean.StartsWith("Version ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[8..].Trim();
        }

        return clean.Length == 0 ? "1.000" : clean;
    }

    private static float ParseVersion(string version)
    {
        string clean = NormaliseVersion(version);
        return float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
            ? value
            : 1f;
    }
}
