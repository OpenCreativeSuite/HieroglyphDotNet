namespace HieroglyphDotNet.Core;

/// <summary>The vertical metrics shared by every glyph in a style.</summary>
public sealed class FontMetrics
{
    /// <summary>The design grid; 1000 units to the em by default.</summary>
    public int UnitsPerEm { get; set; } = 1000;

    /// <summary>The top of the ascender, usually near the em top.</summary>
    public int Ascender { get; set; } = 800;

    /// <summary>The bottom of the descender, negative below the baseline.</summary>
    public int Descender { get; set; } = -200;

    /// <summary>The top of flat capitals.</summary>
    public int CapHeight { get; set; } = 700;

    /// <summary>The top of flat lowercase letters.</summary>
    public int XHeight { get; set; } = 500;

    /// <summary>Extra space between lines, in font units.</summary>
    public int LineGap { get; set; }

    /// <summary>A deep copy.</summary>
    public FontMetrics Clone() => new()
    {
        UnitsPerEm = UnitsPerEm,
        Ascender = Ascender,
        Descender = Descender,
        CapHeight = CapHeight,
        XHeight = XHeight,
        LineGap = LineGap,
    };

    /// <summary>Clamps every value into a range the compiler can write.</summary>
    public void Sanitise()
    {
        UnitsPerEm = Math.Clamp(UnitsPerEm, 16, 16384);
        Ascender = Math.Clamp(Ascender, 1, UnitsPerEm * 2);
        Descender = Math.Clamp(Descender, -UnitsPerEm * 2, -1);
        CapHeight = Math.Clamp(CapHeight, 1, UnitsPerEm * 2);
        XHeight = Math.Clamp(XHeight, 1, UnitsPerEm * 2);
        LineGap = Math.Clamp(LineGap, 0, UnitsPerEm);
    }
}

/// <summary>One weight and width of the family: its own outlines and metrics.</summary>
public sealed class FontStyle
{
    /// <summary>The style name, such as "Regular" or "Bold Italic".</summary>
    public string Name { get; set; } = "Regular";

    /// <summary>The OpenType weight class, 100 to 900.</summary>
    public int Weight { get; set; } = 400;

    /// <summary>The OpenType width class, 1 to 9.</summary>
    public int WidthClass { get; set; } = 5;

    /// <summary>Whether the style is slanted in the metadata.</summary>
    public bool Italic { get; set; }

    /// <summary>The vertical metrics.</summary>
    public FontMetrics Metrics { get; set; } = new();

    /// <summary>Glyphs keyed by code point.</summary>
    public Dictionary<int, Glyph> Glyphs { get; } = [];

    /// <summary>Pair adjustments in font units, keyed by left and right code point.</summary>
    public Dictionary<(int Left, int Right), int> Kerning { get; } = [];

    /// <summary>How many glyphs have an outline.</summary>
    public int DrawnCount => Glyphs.Values.Count(glyph => glyph.HasOutline);

    /// <summary>Looks up a glyph.</summary>
    public Glyph? FindGlyph(int codepoint) => Glyphs.GetValueOrDefault(codepoint);

    /// <summary>The kerning value between two glyphs, or zero.</summary>
    public int Kern(int left, int right) => Kerning.GetValueOrDefault((left, right));

    /// <summary>A copy with independent outlines.</summary>
    public FontStyle Clone(string name)
    {
        FontStyle clone = new()
        {
            Name = name,
            Weight = Weight,
            WidthClass = WidthClass,
            Italic = Italic,
            Metrics = Metrics.Clone(),
        };

        foreach ((int code, Glyph glyph) in Glyphs)
        {
            clone.Glyphs[code] = glyph.Clone();
        }

        foreach (((int left, int right), int value) in Kerning)
        {
            clone.Kerning[(left, right)] = value;
        }

        return clone;
    }

    /// <summary>An empty style with the standard character set laid out at sensible widths.</summary>
    public static FontStyle Create(string name, int weight)
    {
        FontStyle style = new()
        {
            Name = name,
            Weight = weight,
        };

        foreach (CharacterInfo info in CharacterSet.Standard)
        {
            style.Glyphs[info.Codepoint] = Glyph.Blank(
                info.Codepoint,
                info.Name,
                CharacterSet.DefaultAdvance(info.Codepoint));
        }

        return style;
    }
}

/// <summary>A font family: shared metadata plus one or more styles.</summary>
public sealed class FontProject
{
    /// <summary>The family name shown in font menus.</summary>
    public string FamilyName { get; set; } = "Untitled";

    /// <summary>The version string written to the name table.</summary>
    public string Version { get; set; } = "1.000";

    /// <summary>The designer credit.</summary>
    public string Designer { get; set; } = "";

    /// <summary>The copyright line.</summary>
    public string Copyright { get; set; } = "";

    /// <summary>The description written to the name table.</summary>
    public string Description { get; set; } = "";

    /// <summary>The licence note.</summary>
    public string License { get; set; } = "";

    /// <summary>Every style in the family, in menu order.</summary>
    public List<FontStyle> Styles { get; } = [];

    /// <summary>The style being edited.</summary>
    public int ActiveStyleIndex { get; set; }

    /// <summary>The file this project was loaded from, if any.</summary>
    public string? Path { get; set; }

    /// <summary>Whether the project has unsaved edits.</summary>
    public bool Dirty { get; set; }

    /// <summary>The style being edited.</summary>
    public FontStyle ActiveStyle => Styles[Math.Clamp(ActiveStyleIndex, 0, Styles.Count - 1)];

    /// <summary>The family name with the style appended.</summary>
    public string FullName => $"{FamilyName} {ActiveStyle.Name}".Trim();

    /// <summary>The standard sample line used by the preview.</summary>
    public string SampleText { get; set; } = "The quick brown fox jumps over the lazy dog";

    /// <summary>Creates a project with a single Regular style.</summary>
    public static FontProject Create(string familyName)
    {
        FontProject project = new()
        {
            FamilyName = string.IsNullOrWhiteSpace(familyName) ? "Untitled" : familyName.Trim(),
        };

        project.Styles.Add(FontStyle.Create("Regular", 400));
        return project;
    }

    /// <summary>The active style's glyph for a code point.</summary>
    public Glyph? FindGlyph(int codepoint) => ActiveStyle.FindGlyph(codepoint);

    /// <summary>Adds a character to every style when it is not already present.</summary>
    public void EnsureCharacter(int codepoint)
    {
        string name = CharacterSet.NameFor(codepoint);
        foreach (FontStyle style in Styles)
        {
            if (!style.Glyphs.ContainsKey(codepoint))
            {
                style.Glyphs[codepoint] = Glyph.Blank(codepoint, name, CharacterSet.DefaultAdvance(codepoint));
            }
        }
    }

    /// <summary>Removes a character from every style.</summary>
    public void RemoveCharacter(int codepoint)
    {
        foreach (FontStyle style in Styles)
        {
            style.Glyphs.Remove(codepoint);
            foreach ((int left, int right) in style.Kerning.Keys.ToList())
            {
                if (left == codepoint || right == codepoint)
                {
                    style.Kerning.Remove((left, right));
                }
            }
        }
    }

    /// <summary>Every character present in the project, ordered by category then code point.</summary>
    public List<Glyph> CharacterList()
    {
        FontStyle style = ActiveStyle;
        return style.Glyphs.Values
            .OrderBy(glyph => Array.IndexOf(CharacterSet.Order, CharacterSet.CategoryFor(glyph.Codepoint)))
            .ThenBy(glyph => glyph.Codepoint)
            .ToList();
    }

    /// <summary>The number of characters with an outline.</summary>
    public int DrawnCount => ActiveStyle.DrawnCount;

    /// <summary>The number of characters in the repertoire.</summary>
    public int CharacterCount => ActiveStyle.Glyphs.Count;
}
