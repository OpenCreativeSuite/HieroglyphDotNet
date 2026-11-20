using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>The file formats the application can write.</summary>
public enum FontFormat
{
    /// <summary>TrueType outlines in a TrueType container.</summary>
    Ttf,

    /// <summary>Cubic outlines in a CFF-based OpenType container.</summary>
    Otf,

    /// <summary>TrueType outlines in a WOFF 1.0 web container.</summary>
    Woff,

    /// <summary>TrueType outlines in a WOFF 2.0 web container.</summary>
    Woff2,
}

/// <summary>Compiles a style into a real font file.</summary>
public static class FontExporter
{
    /// <summary>The file extension for a format.</summary>
    public static string Extension(FontFormat format) => format switch
    {
        FontFormat.Otf => ".otf",
        FontFormat.Woff => ".woff",
        FontFormat.Woff2 => ".woff2",
        _ => ".ttf",
    };

    /// <summary>A short label for a format.</summary>
    public static string Label(FontFormat format) => format switch
    {
        FontFormat.Otf => "OpenType",
        FontFormat.Woff => "WOFF",
        FontFormat.Woff2 => "WOFF 2.0",
        _ => "TrueType",
    };

    /// <summary>The extension without the dot.</summary>
    public static string ShortLabel(FontFormat format) => Extension(format).TrimStart('.').ToUpperInvariant();

    /// <summary>A one-line description for the format picker.</summary>
    public static string Describe(FontFormat format) => format switch
    {
        FontFormat.Ttf => "Installable everywhere. The safe default.",
        FontFormat.Otf => "Cubic outlines for design software.",
        FontFormat.Woff => "Compressed font for older websites.",
        FontFormat.Woff2 => "Smallest web font. Best for modern sites.",
        _ => "",
    };

    /// <summary>The default file name for a project style.</summary>
    public static string SuggestedFileName(FontProject project, FontStyle style, FontFormat format)
    {
        StringBuilder name = new();
        foreach (char c in (project.FamilyName + "-" + style.Name).Trim())
        {
            name.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        string clean = name.ToString().Trim('-');
        if (clean.Length == 0)
        {
            clean = "Untitled";
        }

        return clean + Extension(format);
    }

    /// <summary>Compiles a style into the chosen container.</summary>
    public static byte[] Compile(FontProject project, FontStyle style, FontFormat format)
    {
        // WOFF 2.0 carries CFF outlines: they need no glyf/loca transform, which
        // keeps the container simple and maximally compatible with sanitizers.
        bool cff = format is FontFormat.Otf or FontFormat.Woff2;
        List<SfntTable> tables = BuildTables(project, style, cff);
        uint flavor = cff ? 0x4F54544Fu : 0x00010000u;

        return format switch
        {
            FontFormat.Woff => SfntBuilder.BuildWoff(flavor, tables),
            FontFormat.Woff2 => SfntBuilder.BuildWoff2(flavor, tables),
            _ => SfntBuilder.Build(flavor, tables),
        };
    }

    /// <summary>The PostScript name written to the name table.</summary>
    public static string PostScriptName(FontProject project, FontStyle style)
    {
        string family = CleanName(project.FamilyName);
        string styleName = CleanName(style.Name);
        if (!family.Any(char.IsLetterOrDigit))
        {
            family = "Untitled";
        }

        if (!styleName.Any(char.IsLetterOrDigit))
        {
            styleName = "Regular";
        }

        string name = family + "-" + styleName;
        return name.Length > 60 ? name[..60] : name;
    }

    private static string CleanName(string text)
    {
        StringBuilder builder = new();
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '.')
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Replace(" ", "");
    }

    private static List<SfntTable> BuildTables(FontProject project, FontStyle style, bool cff)
    {
        FontMetrics metrics = style.Metrics;
        metrics.Sanitise();

        List<Glyph> ordered = [BuildNotDef(metrics.UnitsPerEm)];
        List<(int Code, int Glyph)> mappings = [];
        List<Glyph> characters = style.Glyphs.Values
            .Where(glyph => glyph.Codepoint > 0)
            .OrderBy(glyph => glyph.Codepoint)
            .ToList();

        foreach (Glyph glyph in characters)
        {
            mappings.Add((glyph.Codepoint, ordered.Count));
            ordered.Add(glyph);
        }

        Dictionary<int, int> glyphIds = mappings.GroupBy(pair => pair.Code)
            .ToDictionary(group => group.Key, group => group.First().Glyph);

        List<(int Left, int Right, int Value)> kerning = [];
        foreach (((int left, int right), int value) in style.Kerning)
        {
            if (value == 0 || !glyphIds.TryGetValue(left, out int leftId) || !glyphIds.TryGetValue(right, out int rightId))
            {
                continue;
            }

            kerning.Add((leftId, rightId, value));
        }

        List<int> advances = [];
        List<int> leftBearings = [];
        List<int> rightBearings = [];
        List<int> extents = [];
        Bounds total = Bounds.Empty;

        foreach (Glyph glyph in ordered)
        {
            int advance = Math.Max(0, glyph.AdvanceWidth);
            advances.Add(advance);

            if (!glyph.HasOutline)
            {
                leftBearings.Add(0);
                rightBearings.Add(advance);
                extents.Add(0);
                continue;
            }

            Bounds bounds = glyph.OutlineBounds;
            total = total.Union(bounds);
            int xMin = (int)MathF.Floor(bounds.MinX);
            int xMax = (int)MathF.Ceiling(bounds.MaxX);
            leftBearings.Add(xMin);
            rightBearings.Add(advance - xMax);
            extents.Add(xMax);
        }

        if (total.IsEmpty)
        {
            total = new Bounds(0f, 0f, metrics.UnitsPerEm * 0.5f, metrics.CapHeight);
        }

        short xMin16 = (short)Math.Clamp((int)MathF.Floor(total.MinX), short.MinValue, short.MaxValue);
        short yMin16 = (short)Math.Clamp((int)MathF.Floor(total.MinY), short.MinValue, short.MaxValue);
        short xMax16 = (short)Math.Clamp((int)MathF.Ceiling(total.MaxX), short.MinValue, short.MaxValue);
        short yMax16 = (short)Math.Clamp((int)MathF.Ceiling(total.MaxY), short.MinValue, short.MaxValue);

        int macStyle = (style.Weight >= 700 ? 1 : 0) | (style.Italic ? 2 : 0);
        float italicAngle = style.Italic ? -12f : 0f;

        List<SfntTable> tables =
        [
            new("cmap", OpenTypeTables.Cmap(mappings)),
            new("head", OpenTypeTables.Head(
                project,
                metrics,
                xMin16,
                yMin16,
                xMax16,
                yMax16,
                macStyle,
                cff ? 0 : 1)),
            new("hhea", OpenTypeTables.Hhea(style, advances, leftBearings, rightBearings, extents, italicAngle)),
            new("hmtx", Hmtx(ordered, advances, leftBearings)),
            new("name", OpenTypeTables.Name(project, style, PostScriptName(project, style))),
            new("OS/2", OpenTypeTables.Os2(style, advances, mappings.Select(pair => pair.Code).ToList(), kerning.Count > 0, yMax16, yMin16)),
            new("post", OpenTypeTables.Post(style.Italic, italicAngle)),
        ];

        if (kerning.Count > 0)
        {
            tables.Add(new("kern", OpenTypeTables.Korn(kerning)));
            tables.Add(new("GPOS", OpenTypeTables.Gpos(kerning)));
        }

        if (cff)
        {
            tables.Add(new("CFF ", CffTable.Build(ordered, PostScriptName(project, style), metrics)));
            tables.Add(new("maxp", OpenTypeTables.MaxpCff(ordered.Count)));
        }
        else
        {
            TrueTypeResult outlines = TrueTypeOutlines.Build(ordered);
            int maxPoints = 0;
            int maxContours = 0;
            foreach (Glyph glyph in ordered)
            {
                int points = 0;
                int contours = 0;
                foreach (GlyphContour contour in glyph.Contours)
                {
                    if (contour.Nodes.Count < 2)
                    {
                        continue;
                    }

                    points += TrueTypeOutlines.QuadraticContour(contour).Count;
                    contours++;
                }

                maxPoints = Math.Max(maxPoints, points);
                maxContours = Math.Max(maxContours, contours);
            }

            tables.Add(new("glyf", outlines.Glyf));
            tables.Add(new("loca", outlines.Loca));
            tables.Add(new("maxp", OpenTypeTables.Maxp(ordered.Count, maxPoints, maxContours)));
        }

        return tables;
    }

    private static byte[] Hmtx(IReadOnlyList<Glyph> glyphs, IReadOnlyList<int> advances, IReadOnlyList<int> leftBearings)
    {
        ByteWriter writer = new();
        for (int i = 0; i < glyphs.Count; i++)
        {
            writer.U16((ushort)Math.Clamp(advances[i], 0, ushort.MaxValue));
            writer.I16((short)Math.Clamp(leftBearings[i], short.MinValue, short.MaxValue));
        }

        return writer.ToArray();
    }

    private static Glyph BuildNotDef(int unitsPerEm)
    {
        OutlineBuilder builder = new();
        float right = unitsPerEm * 0.52f;
        float top = unitsPerEm * 0.72f;
        builder.Rect(unitsPerEm * 0.04f, 0f, right, top);
        builder.Polygon(
        [
            new Vec2(unitsPerEm * 0.12f, unitsPerEm * 0.08f),
            new Vec2(unitsPerEm * 0.12f, top - (unitsPerEm * 0.08f)),
            new Vec2(right - (unitsPerEm * 0.08f), top - (unitsPerEm * 0.08f)),
            new Vec2(right - (unitsPerEm * 0.08f), unitsPerEm * 0.08f),
        ], hole: true);

        return new Glyph
        {
            Codepoint = 0,
            Name = ".notdef",
            AdvanceWidth = (int)right + 40,
            Contours = [.. builder.Contours],
        };
    }
}
