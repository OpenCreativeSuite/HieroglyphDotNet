using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class ProjectFileTests
{
    [Fact]
    public void ProjectRoundTripsMetadataStylesGlyphsAndKerning()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.Project.FamilyName = "Round Trip";
        editor.Project.Version = "2.001";
        editor.Project.Designer = "Designer";
        editor.Project.Copyright = "Copyright";
        editor.Project.Description = "Description";
        editor.Project.License = "License";
        editor.Project.SampleText = "Sample";

        OutlineBuilder builder = new();
        builder.Rect(10, 20, 300, 700);
        editor.Project.EnsureCharacter(0x2603);
        Glyph custom = editor.Style.Glyphs[0x2603];
        custom.Contours = [.. builder.Contours];
        custom.Contours[0].Nodes[0].HasOut = true;
        custom.Contours[0].Nodes[0].Out = new Vec2(20, 30);
        custom.Contours[0].Nodes[0].Smooth = true;
        custom.Contours[0].Nodes[0].HasIn = true;
        custom.Contours[0].Nodes[0].In = new Vec2(5, 6);
        custom.AdvanceWidth = 777;
        editor.Style.Kerning[(0x2603, 'A')] = -42;
        editor.AddStyle("Bold", 700, italic: true);
        editor.SetActiveStyle(0);

        string path = temp.File("font.hgl");
        ProjectFile.SaveTo(editor.Project, path);

        Assert.Equal(path, editor.Project.Path);
        Assert.False(editor.Project.Dirty);
        FontProject loaded = ProjectFile.Load(path);

        Assert.Equal("Round Trip", loaded.FamilyName);
        Assert.Equal("2.001", loaded.Version);
        Assert.Equal("Designer", loaded.Designer);
        Assert.Equal("Copyright", loaded.Copyright);
        Assert.Equal("Description", loaded.Description);
        Assert.Equal("License", loaded.License);
        Assert.Equal("Sample", loaded.SampleText);
        Assert.Equal(2, loaded.Styles.Count);
        Assert.Equal("Bold", loaded.Styles[1].Name);
        Assert.Equal(700, loaded.Styles[1].Weight);
        Assert.True(loaded.Styles[1].Italic);
        Assert.Equal(path, loaded.Path);
        Assert.False(loaded.Dirty);

        Glyph restored = loaded.ActiveStyle.Glyphs[0x2603];
        Assert.Equal(777, restored.AdvanceWidth);
        Assert.Single(restored.Contours);
        GlyphNode node = restored.Contours[0].Nodes[0];
        Assert.Equal(10f, node.Point.X, 2);
        Assert.Equal(20f, node.Point.Y, 2);
        Assert.Equal(20f, node.Out.X, 2);
        Assert.Equal(30f, node.Out.Y, 2);
        Assert.Equal(5f, node.In.X, 2);
        Assert.Equal(6f, node.In.Y, 2);
        Assert.True(node.Smooth);
        Assert.Equal(-42, loaded.ActiveStyle.Kern(0x2603, 'A'));
    }

    [Fact]
    public void LoadGuaranteesTheStandardRepertoireAndClampsStyles()
    {
        using TempDirectory temp = new();
        string json = """
            {
              "FamilyName": "Tiny",
              "ActiveStyle": 5,
              "Styles": [
                {
                  "Name": "",
                  "Weight": 5,
                  "WidthClass": 99,
                  "Glyphs": [ { "Code": 65, "Name": "", "Advance": 500 } ]
                }
              ]
            }
            """;
        string path = temp.File("tiny.hgl");
        File.WriteAllText(path, json);

        FontProject project = ProjectFile.Load(path);

        Assert.Equal("Tiny", project.FamilyName);
        Assert.Single(project.Styles);
        Assert.Equal(0, project.ActiveStyleIndex);
        Assert.Equal("Regular", project.ActiveStyle.Name);
        Assert.Equal(100, project.ActiveStyle.Weight);
        Assert.Equal(9, project.ActiveStyle.WidthClass);
        Assert.True(project.ActiveStyle.Glyphs.Count >= CharacterSet.Standard.Count);
        Assert.Equal("Capital A", project.ActiveStyle.Glyphs['A'].Name);
    }

    [Fact]
    public void InvalidProjectFilesThrow()
    {
        using TempDirectory temp = new();

        string empty = temp.File("empty.hgl");
        File.WriteAllText(empty, """{ "Styles": [] }""");
        Assert.Throws<InvalidDataException>(() => ProjectFile.Load(empty));

        string broken = temp.File("broken.hgl");
        File.WriteAllText(broken, "not json");
        Assert.ThrowsAny<Exception>(() => ProjectFile.Load(broken));
    }

    [Fact]
    public void SaveCreatesTheParentFolderAndExtensionsAreExposed()
    {
        using TempDirectory temp = new();
        string path = temp.File("nested/folder/font.hgl");

        ProjectFile.SaveTo(FontProject.Create("Created"), path);

        Assert.True(File.Exists(path));
        Assert.Equal(".hgl", ProjectFile.Extension);
        Assert.Contains(".hgl", ProjectFile.Extensions);
        Assert.Contains(".json", ProjectFile.Extensions);
    }
}

public sealed class FontExporterTests
{
    [Fact]
    public void MetadataHelpersDescribeEveryFormat()
    {
        Assert.Equal(".ttf", FontExporter.Extension(FontFormat.Ttf));
        Assert.Equal(".otf", FontExporter.Extension(FontFormat.Otf));
        Assert.Equal(".woff", FontExporter.Extension(FontFormat.Woff));
        Assert.Equal(".woff2", FontExporter.Extension(FontFormat.Woff2));
        Assert.Equal("TrueType", FontExporter.Label(FontFormat.Ttf));
        Assert.Equal("OpenType", FontExporter.Label(FontFormat.Otf));
        Assert.Equal("TTF", FontExporter.ShortLabel(FontFormat.Ttf));
        Assert.False(string.IsNullOrWhiteSpace(FontExporter.Describe(FontFormat.Woff2)));
    }

    [Fact]
    public void SuggestedNameAndPostScriptNameAreClean()
    {
        FontProject project = FontProject.Create("My Typeface!");
        FontStyle style = project.ActiveStyle;

        Assert.Equal("My-Typeface--Regular.ttf", FontExporter.SuggestedFileName(project, style, FontFormat.Ttf));

        project.FamilyName = "A B";
        Assert.Equal("AB-Regular", FontExporter.PostScriptName(project, style));

        project.FamilyName = "!!!";
        Assert.Equal("Untitled-Regular", FontExporter.PostScriptName(project, style));
    }

    [Fact]
    public void SampleFontCompilesToAllFourFormats()
    {
        FontEditor editor = Fx.Sample();

        byte[] ttf = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf);
        byte[] otf = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Otf);
        byte[] woff = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Woff);
        byte[] woff2 = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Woff2);

        Assert.True(ttf.Length > 1000);
        Assert.True(otf.Length > 1000);
        Assert.True(woff.Length > 1000);
        Assert.True(woff2.Length > 1000);

        Assert.Equal(0x00010000u, U32(ttf, 0));
        Assert.Equal("OTTO"u8.ToArray(), otf.Take(4));
        Assert.Equal("wOFF"u8.ToArray(), woff.Take(4));
        Assert.Equal("wOF2"u8.ToArray(), woff2.Take(4));

        List<string> tags = TableTags(ttf);
        Assert.Contains("cmap", tags);
        Assert.Contains("head", tags);
        Assert.Contains("glyf", tags);
        Assert.Contains("loca", tags);
        Assert.Contains("hmtx", tags);
        Assert.Contains("name", tags);
        Assert.Contains("OS/2", tags);
        Assert.Contains("post", tags);

        List<string> otfTags = TableTags(otf);
        Assert.Contains("CFF ", otfTags);
        Assert.DoesNotContain("glyf", otfTags);
    }

    [Fact]
    public void KerningAddsKernAndGposTables()
    {
        FontEditor editor = Fx.Sample();
        Assert.NotEmpty(editor.Style.Kerning);

        byte[] ttf = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf);

        Assert.Contains("kern", TableTags(ttf));
        Assert.Contains("GPOS", TableTags(ttf));
    }

    [Fact]
    public void CompiledFontCanBeWrittenToDisk()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        byte[] bytes = FontExporter.Compile(editor.Project, editor.Style, FontFormat.Ttf);
        string path = temp.File("font.ttf");

        File.WriteAllBytes(path, bytes);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    private static uint U32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static List<string> TableTags(byte[] sfnt)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4, 2));
        List<string> tags = [];
        for (int i = 0; i < count; i++)
        {
            int at = 12 + (i * 16);
            tags.Add(Encoding.ASCII.GetString(sfnt, at, 4));
        }

        return tags;
    }
}

public sealed class DemoFontTests
{
    [Fact]
    public void SampleHasACompleteAlphabetAndKerning()
    {
        FontProject project = DemoFont.Create();

        Assert.Equal("Occam", project.FamilyName);
        Assert.Single(project.Styles);
        Assert.Equal("Regular", project.ActiveStyle.Name);
        Assert.True(project.CharacterCount >= CharacterSet.Standard.Count);
        Assert.True(project.DrawnCount > 60);
        Assert.True(project.ActiveStyle.Kerning.Count > 20);

        for (int code = 'A'; code <= 'Z'; code++)
        {
            Assert.True(project.ActiveStyle.Glyphs[code].HasOutline);
        }

        for (int code = 'a'; code <= 'z'; code++)
        {
            Assert.True(project.ActiveStyle.Glyphs[code].HasOutline);
        }

        for (int code = '0'; code <= '9'; code++)
        {
            Assert.True(project.ActiveStyle.Glyphs[code].HasOutline);
        }
    }

    [Fact]
    public void SampleMetricsAndKerningAreSensible()
    {
        FontProject project = DemoFont.Create();
        FontStyle style = project.ActiveStyle;

        Assert.Equal(1000, style.Metrics.UnitsPerEm);
        Assert.Equal(700, style.Metrics.CapHeight);
        Assert.Equal(500, style.Metrics.XHeight);
        Assert.Equal(-70, style.Kern('A', 'V'));
        Assert.Equal(-90, style.Kern('T', 'o'));
        Assert.Equal(0, style.Kern('A', 'A'));
    }
}

public sealed class CharacterSetBuildTests
{
    [Fact]
    public void FontProjectCommandsAddRemoveAndListCharacters()
    {
        FontProject project = FontProject.Create("Test");
        project.EnsureCharacter(0x2603);
        Assert.True(project.ActiveStyle.Glyphs.ContainsKey(0x2603));

        project.ActiveStyle.Kerning[(0x2603, 'A')] = -30;
        project.RemoveCharacter(0x2603);
        Assert.False(project.ActiveStyle.Glyphs.ContainsKey(0x2603));
        Assert.Empty(project.ActiveStyle.Kerning);

        List<Glyph> list = project.CharacterList();
        Assert.Equal('A', list[0].Codepoint);
        Assert.Equal(project.ActiveStyle.Glyphs.Count, list.Count);
    }

    [Fact]
    public void StyleCloneCopiesGlyphsKerningAndMetrics()
    {
        FontProject project = DemoFont.Create();
        FontStyle source = project.ActiveStyle;

        FontStyle clone = source.Clone("Copy");
        clone.Glyphs['A'].Contours.Clear();
        clone.Metrics.XHeight = 1;

        Assert.Equal("Copy", clone.Name);
        Assert.NotSame(source.Glyphs['A'], clone.Glyphs['A']);
        Assert.True(source.Glyphs['A'].HasOutline);
        Assert.Equal(500, source.Metrics.XHeight);
        Assert.Equal(source.Kerning.Count, clone.Kerning.Count);
    }
}
