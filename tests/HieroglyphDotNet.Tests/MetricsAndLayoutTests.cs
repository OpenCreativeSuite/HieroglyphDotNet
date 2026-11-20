using System.Text;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class FontMetricsTests
{
    [Fact]
    public void DefaultsDescribeAThousandUnitEm()
    {
        FontMetrics metrics = new();

        Assert.Equal(1000, metrics.UnitsPerEm);
        Assert.Equal(800, metrics.Ascender);
        Assert.Equal(-200, metrics.Descender);
        Assert.Equal(700, metrics.CapHeight);
        Assert.Equal(500, metrics.XHeight);
        Assert.Equal(0, metrics.LineGap);
    }

    [Fact]
    public void CloneIsIndependent()
    {
        FontMetrics source = new() { UnitsPerEm = 2048, XHeight = 900 };
        FontMetrics clone = source.Clone();
        clone.XHeight = 1;

        Assert.Equal(2048, clone.UnitsPerEm);
        Assert.Equal(900, source.XHeight);
        Assert.Equal(1, clone.XHeight);
    }

    [Fact]
    public void SanitiseClampsEveryValueToACompilableRange()
    {
        FontMetrics metrics = new()
        {
            UnitsPerEm = 1,
            Ascender = -5,
            Descender = 100,
            CapHeight = 99999,
            XHeight = 0,
            LineGap = 99999,
        };

        metrics.Sanitise();

        Assert.Equal(16, metrics.UnitsPerEm);
        Assert.Equal(1, metrics.Ascender);
        Assert.Equal(-1, metrics.Descender);
        Assert.Equal(32, metrics.CapHeight);
        Assert.Equal(1, metrics.XHeight);
        Assert.Equal(16, metrics.LineGap);

        metrics.UnitsPerEm = 99999;
        metrics.Sanitise();
        Assert.Equal(16384, metrics.UnitsPerEm);
    }
}

public sealed class FontProjectModelTests
{
    [Fact]
    public void CreateUsesTheFallbackFamilyAndStandardRepertoire()
    {
        FontProject project = FontProject.Create("   ");

        Assert.Equal("Untitled", project.FamilyName);
        Assert.Single(project.Styles);
        Assert.Equal("Regular", project.ActiveStyle.Name);
        Assert.Equal(400, project.ActiveStyle.Weight);
        Assert.Equal(5, project.ActiveStyle.WidthClass);
        Assert.Equal(CharacterSet.Standard.Count, project.CharacterCount);
        Assert.Equal(0, project.DrawnCount);
        Assert.Equal("Untitled Regular", project.FullName);
        Assert.Equal("The quick brown fox jumps over the lazy dog", project.SampleText);
    }

    [Fact]
    public void ActiveStyleIsClampedAndFullNameTrims()
    {
        FontProject project = FontProject.Create("Family");
        project.ActiveStyle.Name = "Book";
        project.ActiveStyleIndex = 99;

        Assert.Same(project.Styles[0], project.ActiveStyle);
        Assert.Equal("Family Book", project.FullName);

        project.FamilyName = "";
        Assert.Equal("Book", project.FullName);
    }

    [Fact]
    public void EnsureCharacterDoesNotReplaceExistingOutlines()
    {
        FontProject project = FontProject.Create("Family");
        project.ActiveStyle.Glyphs['A'].Contours.Add(new GlyphContour
        {
            Nodes = [GlyphNode.Corner(new Vec2(1, 2)), GlyphNode.Corner(new Vec2(3, 4))],
        });
        int nodes = project.ActiveStyle.Glyphs['A'].NodeCount;

        project.EnsureCharacter('A');
        project.EnsureCharacter(0x2603);

        Assert.Equal(nodes, project.ActiveStyle.Glyphs['A'].NodeCount);
        Assert.True(project.ActiveStyle.Glyphs.ContainsKey(0x2603));
        Assert.Equal(600, project.ActiveStyle.Glyphs[0x2603].AdvanceWidth);
    }

    [Fact]
    public void RemoveCharacterCleansKerningOnBothSides()
    {
        FontProject project = FontProject.Create("Family");
        project.ActiveStyle.Kerning[('A', 'V')] = -20;
        project.ActiveStyle.Kerning[('T', 'A')] = -30;
        project.ActiveStyle.Kerning[('T', 'o')] = -40;

        project.RemoveCharacter('A');

        Assert.False(project.ActiveStyle.Glyphs.ContainsKey('A'));
        Assert.Equal(0, project.ActiveStyle.Kern('A', 'V'));
        Assert.Equal(0, project.ActiveStyle.Kern('T', 'A'));
        Assert.Equal(-40, project.ActiveStyle.Kern('T', 'o'));
    }

    [Fact]
    public void CharacterListOrdersByCategoryThenCode()
    {
        FontProject project = FontProject.Create("Family");
        project.EnsureCharacter(0x2603);

        List<Glyph> list = project.CharacterList();

        Assert.Equal('A', list[0].Codepoint);
        Assert.Equal(0x2603, list[^1].Codepoint);
    }

    [Fact]
    public void FindGlyphAndKernUseTheActiveStyle()
    {
        FontProject project = FontProject.Create("Family");
        project.ActiveStyle.Kerning[('A', 'V')] = -55;

        Assert.Same(project.ActiveStyle.Glyphs['A'], project.FindGlyph('A'));
        Assert.Null(project.FindGlyph(0x2603));
        Assert.Equal(-55, project.ActiveStyle.Kern('A', 'V'));
        Assert.Equal(0, project.ActiveStyle.Kern('V', 'A'));
    }
}

public sealed class FontValidatorDetailTests
{
    [Fact]
    public void InvalidUnitsPerEmIsAnError()
    {
        FontEditor editor = Fx.Sample();
        editor.Style.Metrics.UnitsPerEm = 1;

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.Contains(issues, issue => issue.Level == IssueLevel.Error && issue.Message.Contains("Units per em"));
        Assert.False(FontValidator.CanExport(issues));
    }

    [Fact]
    public void BlankCharactersProduceAWarningWithExamples()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        FontIssue blank = issues.First(issue => issue.Message.Contains("characters are still blank"));
        Assert.Equal(IssueLevel.Warning, blank.Level);
        Assert.Contains("For example", blank.Detail);
        Assert.True(FontValidator.CanExport(issues));
    }

    [Fact]
    public void OpenThinAndOverflowingOutlinesAreReported()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 400, 0, 700, 700, advance: 500);
        glyph.Contours[0].Closed = false;
        Glyph thin = editor.Style.Glyphs['B'];
        thin.Contours.Add(new GlyphContour
        {
            Nodes = [GlyphNode.Corner(Vec2.Zero), GlyphNode.Corner(new Vec2(1, 1))],
        });

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.Contains(issues, issue => issue.Message.Contains("open contour"));
        Assert.Contains(issues, issue => issue.Message.Contains("too few points"));
        Assert.Contains(issues, issue => issue.Message.Contains("extend past their advance"));
    }

    [Fact]
    public void NoAdvanceAndKerningNotesAreReported()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        glyph.AdvanceWidth = 0;
        editor.Style.Kerning[('A', 'V')] = -40;

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.Contains(issues, issue => issue.Message.Contains("no advance"));
        Assert.Contains(issues, issue => issue.Level == IssueLevel.Info && issue.Message.Contains("kerning pairs"));
        Assert.True(FontValidator.CanExport(issues));
    }

    [Fact]
    public void ACleanFontReportsThatEverythingChecksOut()
    {
        FontEditor editor = Fx.Editor();
        foreach (Glyph glyph in editor.Style.Glyphs.Values)
        {
            glyph.Contours.Add(new GlyphContour
            {
                Nodes =
                [
                    GlyphNode.Corner(new Vec2(0, 0)),
                    GlyphNode.Corner(new Vec2(100, 0)),
                    GlyphNode.Corner(new Vec2(100, 100)),
                    GlyphNode.Corner(new Vec2(0, 100)),
                ],
            });
            glyph.AdvanceWidth = 120;
        }

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.DoesNotContain(issues, issue => issue.Level is IssueLevel.Error or IssueLevel.Warning);
        Assert.Contains(issues, issue => issue.Message.Contains("Everything checks out"));
    }

    [Fact]
    public void CanExportOnlyRejectsErrors()
    {
        Assert.True(FontValidator.CanExport([new FontIssue(IssueLevel.Info, "ok")]));
        Assert.True(FontValidator.CanExport([new FontIssue(IssueLevel.Warning, "hmm")]));
        Assert.False(FontValidator.CanExport([new FontIssue(IssueLevel.Error, "no")]));
    }
}

public sealed class TypefaceLayoutTests
{
    [Fact]
    public void TrackingIsAddedToEveryGlyphAdvance()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;
        Glyph a = style.FindGlyph('A')!;
        Glyph b = style.FindGlyph('B')!;

        Fx.Nearly(a.AdvanceWidth + 20f, Typeface.Advance(style, a, 1f, 20f));
        Fx.Nearly(b.AdvanceWidth + 40f, Typeface.Advance(style, b, 1f, 40f));
    }

    [Fact]
    public void MeasureAppliesNegativeKerningBetweenPairs()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;

        float unkerned = Typeface.Advance(style, style.FindGlyph('A')!, 1f, 0f)
            + Typeface.Advance(style, style.FindGlyph('V')!, 1f, 0f);
        float kerned = Typeface.Measure(style, "AV", 1f, 0f);

        Assert.True(kerned < unkerned);
        Fx.Nearly(unkerned + style.Kern('A', 'V'), kerned);
    }

    [Fact]
    public void MissingCharactersUseTheFallbackAdvance()
    {
        FontStyle style = FontProject.Create("Fallback").ActiveStyle;

        Fx.Nearly(300f, Typeface.Measure(style, "☃", 1f, 0f));
        Fx.Nearly(300f, Typeface.Measure(style, "A☃A", 1f, 0f)
            - Typeface.Measure(style, "AA", 1f, 0f));
    }

    [Fact]
    public void NewlinesDoNotAddWidthButParagraphsAreKept()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;

        Fx.Nearly(Typeface.Measure(style, "AB", 1f, 0f), Typeface.Measure(style, "A\nB", 1f, 0f));
        Assert.Equal(["One", "", "Two"], Typeface.Wrap(style, "One\n\nTwo", 0.1f, 0f, 1000f));
    }

    [Fact]
    public void WrapKeepsLongWordsIntactAndSplitsOnSpaces()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;

        List<string> lines = Typeface.Wrap(style, "Supercalifragilisticexpialidocious", 0.5f, 0f, 20f);
        Assert.Single(lines);
        Assert.Equal("Supercalifragilisticexpialidocious", lines[0]);

        List<string> words = Typeface.Wrap(style, "one two three four", 0.5f, 0f, 200f);
        Assert.True(words.Count > 1);
        Assert.All(words, line => Assert.DoesNotContain("  ", line));
    }

    [Fact]
    public void WrapEmptyStringReturnsOneEmptyLine()
    {
        FontEditor editor = Fx.Sample();

        Assert.Equal([""], Typeface.Wrap(editor.Style, "", 1f, 0f, 100f));
    }
}

public sealed class FontExporterDetailTests
{
    [Fact]
    public void SuggestedFileNamesSanitiseBothFamilyAndStyle()
    {
        FontProject project = FontProject.Create("My / Fancy : Face");
        project.ActiveStyle.Name = "Bold Italic";

        string name = FontExporter.SuggestedFileName(project, project.ActiveStyle, FontFormat.Otf);

        Assert.Equal("My---Fancy---Face-Bold-Italic.otf", name);
    }

    [Fact]
    public void PostScriptNamesAreTruncatedToSixtyCharacters()
    {
        FontProject project = FontProject.Create(new string('A', 80));
        project.ActiveStyle.Name = "Regular";

        string name = FontExporter.PostScriptName(project, project.ActiveStyle);

        Assert.Equal(60, name.Length);
        Assert.StartsWith("AAAA", name);
    }

    [Fact]
    public void PostScriptNameFallsBackForPunctuationOnlyNames()
    {
        FontProject project = FontProject.Create("!!!");
        project.ActiveStyle.Name = "***";

        Assert.Equal("Untitled-Regular", FontExporter.PostScriptName(project, project.ActiveStyle));
    }

    [Fact]
    public void DescriptionsCoverEveryFormat()
    {
        foreach (FontFormat format in Enum.GetValues<FontFormat>())
        {
            Assert.NotEmpty(FontExporter.Describe(format));
            Assert.NotEmpty(FontExporter.Label(format));
            Assert.NotEmpty(FontExporter.ShortLabel(format));
            Assert.StartsWith(".", FontExporter.Extension(format));
        }
    }

    [Fact]
    public void CompilingAnEmptyProjectStillWritesANotdef()
    {
        FontProject project = FontProject.Create("Empty");

        byte[] ttf = FontExporter.Compile(project, project.ActiveStyle, FontFormat.Ttf);
        byte[] otf = FontExporter.Compile(project, project.ActiveStyle, FontFormat.Otf);

        Assert.True(ttf.Length > 100);
        Assert.True(otf.Length > 100);
        Assert.Equal("OTTO"u8.ToArray(), otf[..4]);
    }

    [Fact]
    public void CompiledCmapIncludesTheDrawnCharacters()
    {
        FontEditor editor = Fx.Sample();
        byte[] ttf = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf);
        byte[] cmap = Table(ttf, "cmap");

        int format12 = FindFormat12(cmap);
        Assert.True(format12 > 0);
        int groups = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cmap.AsSpan(format12 + 12));
        Assert.True(groups > 0);
        uint firstStart = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cmap.AsSpan(format12 + 16));
        Assert.True(firstStart <= 0x20);
    }

    [Fact]
    public void CompiledNameTableContainsTheFamilyAndStyle()
    {
        FontEditor editor = Fx.Sample();
        byte[] ttf = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf);
        byte[] name = Table(ttf, "name");
        string text = Encoding.BigEndianUnicode.GetString(name);

        Assert.Contains("Occam", text);
        Assert.Contains("Regular", text);
    }

    [Fact]
    public void WebWrappersCarryTheOriginalFlavor()
    {
        FontEditor editor = Fx.Sample();

        byte[] woff = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Woff);
        byte[] woff2 = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Woff2);

        uint ttfFlavor = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
            FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf));
        uint otfFlavor = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
            FontExporter.Compile(editor.Project, editor.Style, FontFormat.Otf));
        Assert.Equal(ttfFlavor, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(4)));
        Assert.Equal(otfFlavor, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(4)));
    }

    private static byte[] Table(byte[] sfnt, string tag)
    {
        int count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            int at = 12 + (i * 16);
            if (Encoding.ASCII.GetString(sfnt, at, 4) == tag)
            {
                int offset = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(at + 8));
                int length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(at + 12));
                return sfnt[offset..(offset + length)];
            }
        }

        throw new InvalidOperationException("Table not found: " + tag);
    }

    private static int FindFormat12(byte[] cmap)
    {
        int records = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(2));
        for (int i = 0; i < records; i++)
        {
            int at = 4 + (i * 8);
            int offset = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cmap.AsSpan(at + 4));
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(offset)) == 12)
            {
                return offset;
            }
        }

        return -1;
    }
}
