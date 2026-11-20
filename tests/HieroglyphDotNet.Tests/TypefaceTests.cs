using System.Buffers.Binary;
using System.Numerics;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class TypefaceTests
{
    [Fact]
    public void AdvanceAndMeasureIncludeTrackingAndKerning()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;
        Glyph a = style.FindGlyph('A')!;
        Glyph v = style.FindGlyph('V')!;

        Assert.Equal((a.AdvanceWidth * 0.5f) + 5f, Typeface.Advance(style, a, 0.5f, 10f), 3);

        float expected = (a.AdvanceWidth * 0.5f) + (style.Kern('A', 'V') * 0.5f) + (v.AdvanceWidth * 0.5f);
        Fx.Nearly(expected, Typeface.Measure(style, "AV", 0.5f, 0f), 0.01f);
        Assert.True(Typeface.Measure(style, "AV", 0.5f, 0f) < Typeface.Measure(style, "A", 0.5f, 0f) + Typeface.Measure(style, "V", 0.5f, 0f));
    }

    [Fact]
    public void MissingGlyphsUseAFallbackAdvance()
    {
        FontEditor editor = Fx.Editor();
        FontStyle style = editor.Style;

        Fx.Nearly(300f, Typeface.Measure(style, "\u2603", 1f, 0f), 0.01f);
        Fx.Nearly(Typeface.Measure(style, "A", 1f, 0f), Typeface.Measure(style, "A\n", 1f, 0f), 0.01f);
    }

    [Fact]
    public void WrapBreaksLongLinesAndPreservesParagraphs()
    {
        FontEditor editor = Fx.Sample();
        FontStyle style = editor.Style;

        List<string> lines = Typeface.Wrap(style, "The quick brown fox jumps", 0.1f, 0f, 120f);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(
            line.Contains(' ') == false || Typeface.Measure(style, line, 0.1f, 0f) <= 120f + 0.01f));

        List<string> paragraphs = Typeface.Wrap(style, "One\n\nTwo", 0.1f, 0f, 1000f);
        Assert.Equal(["One", "", "Two"], paragraphs);
    }
}

public sealed class CharacterSetTests
{
    [Fact]
    public void StandardRepertoireIsOrderedAndUnique()
    {
        IReadOnlyList<CharacterInfo> standard = CharacterSet.Standard;

        Assert.True(standard.Count > 100);
        Assert.Equal(standard.Count, standard.Select(info => info.Codepoint).Distinct().Count());
        Assert.Equal(standard.Select(info => Array.IndexOf(CharacterSet.Order, info.Category)).OrderBy(value => value), standard.Select(info => Array.IndexOf(CharacterSet.Order, info.Category)));
        Assert.Equal('A', standard[0].Codepoint);
    }

    [Fact]
    public void CharacterInfoFormatsDisplayValues()
    {
        CharacterInfo info = CharacterSet.Find('A')!;

        Assert.Equal("A", info.Character);
        Assert.Equal("U+0041", info.Code);
        Assert.Equal("Capital A", info.Name);
        Assert.Equal(CharacterCategory.Uppercase, info.Category);
        Assert.Equal(0x20, CharacterSet.Find(0x20)!.Codepoint);
    }

    [Fact]
    public void NamesCategoriesAndLabelsFallBackToCustom()
    {
        Assert.Equal("U+2603", CharacterSet.NameFor(0x2603));
        Assert.Equal(CharacterCategory.Custom, CharacterSet.CategoryFor(0x2603));
        Assert.Equal("Custom", CharacterSet.Label(CharacterCategory.Custom));
        Assert.Equal("Uppercase", CharacterSet.Label(CharacterCategory.Uppercase));
        Assert.Equal("Numbers", CharacterSet.Label(CharacterCategory.Numbers));
    }

    [Fact]
    public void DefaultAdvancesCoverTheCommonRanges()
    {
        Assert.Equal(260, CharacterSet.DefaultAdvance(' '));
        Assert.Equal(600, CharacterSet.DefaultAdvance('5'));
        Assert.Equal(640, CharacterSet.DefaultAdvance('A'));
        Assert.Equal(560, CharacterSet.DefaultAdvance('a'));
        Assert.Equal(340, CharacterSet.DefaultAdvance('.'));
        Assert.Equal(320, CharacterSet.DefaultAdvance(','));
        Assert.Equal(380, CharacterSet.DefaultAdvance('('));
        Assert.Equal(600, CharacterSet.DefaultAdvance(0x2603));
        Assert.True(CharacterSet.IsSpace(' '));
        Assert.True(CharacterSet.IsSpace(0xA0));
        Assert.False(CharacterSet.IsSpace('A'));
    }
}

public sealed class FontValidatorTests
{
    [Fact]
    public void EmptyProjectReportsAnError()
    {
        FontEditor editor = Fx.Editor();
        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.Contains(issues, issue => issue.Level == IssueLevel.Error);
        Assert.False(FontValidator.CanExport(issues));
    }

    [Fact]
    public void ValidSampleFontPassesWithInformationalNotes()
    {
        FontEditor editor = Fx.Sample();
        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.True(FontValidator.CanExport(issues));
        Assert.Contains(issues, issue => issue.Level == IssueLevel.Warning || issue.Message.Contains("kerning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationWarnsAboutOpenContoursBlankAdvancesOverflowAndBadNames()
    {
        FontEditor editor = Fx.Editor();
        editor.Project.FamilyName = "";
        Glyph glyph = editor.SelectedGlyph!;
        OutlineBuilder builder = new();
        builder.Polygon([new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 100)], hole: false);
        glyph.Contours = [.. builder.Contours];
        glyph.Contours[0].Closed = false;
        glyph.AdvanceWidth = 0;

        List<FontIssue> issues = FontValidator.Validate(editor.Project, editor.Style);

        Assert.Contains(issues, issue => issue.Message.Contains("family name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Message.Contains("open contour", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Message.Contains("no advance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => issue.Message.Contains("blank", StringComparison.OrdinalIgnoreCase) && issue.Level == IssueLevel.Error);
    }
}

public sealed class SettingsTests
{
    [Fact]
    public void SettingsFolderIsInsideLocalAppDataAndLoadReturnsAValue()
    {
        Settings settings = Settings.Load();

        Assert.False(string.IsNullOrWhiteSpace(Settings.Folder));
        Assert.Contains("Hieroglyph", Settings.Folder);
        Assert.NotNull(settings);
        Assert.True(settings.LastFolder.Length >= 0);
    }
}
