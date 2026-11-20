using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class PanelTests
{
    [Fact]
    public void PanelRendersHeaderBadgeAndBodyWhenOpen()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Panel, PanelProps>(new PanelProps
        {
            Title = "Sample",
            Badge = "3",
            StartOpen = true,
            Children = [Div(new() { ClassName = "sample-body" }, Text("Body"))],
        }));

        Assert.Equal("Sample", page.Find(".panel-title").Text);
        Assert.Equal("3", page.Find(".panel-badge").Text);
        Assert.NotNull(page.TryFind(".panel-body"));
        Assert.NotNull(page.TryFind(".fa-chevron-down"));
        Assert.NotNull(page.Find(".sample-body"));
    }

    [Fact]
    public void PanelCollapsesAndExpandsWhenTheHeaderIsClicked()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Panel, PanelProps>(new PanelProps
        {
            Title = "Sample",
            StartOpen = true,
            Children = [Text("Body")],
        }));

        page.Click(".panel-head");
        Assert.Null(page.TryFind(".panel-body"));
        Assert.NotNull(page.TryFind(".fa-chevron-right"));

        page.Click(".panel-head");
        Assert.NotNull(page.TryFind(".panel-body"));
    }

    [Fact]
    public void PanelCanStartClosed()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Panel, PanelProps>(new PanelProps
        {
            Title = "Closed",
            StartOpen = false,
            Children = [Text("Hidden")],
        }));

        Assert.Null(page.TryFind(".panel-body"));
    }
}

public sealed class InspectorPanelTests
{
    [Fact]
    public void InspectorShowsTheSelectedCharacterAsItsSubject()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<InspectorPanel>());

        Assert.Equal("Inspector", page.Find(".side-head-title").Text);
        Assert.Equal("A", page.Find(".side-head-name").Text);
        Assert.Equal(4, page.FindAll(".panel").Count);
    }

    [Fact]
    public void SubjectFollowsSelectionAndFallsBackToFont()
    {
        FontEditor editor = Fx.Editor();
        editor.SelectGlyph(0x20);
        var page = Fx.Render(editor, C<InspectorPanel>());
        Assert.Equal(" ", page.Find(".side-head-name").Text);

        editor.SelectGlyph(0x2603);
        Fx.Reload<InspectorPanel>(page, editor);
        Assert.Equal("☃", page.Find(".side-head-name").Text);

        editor.Style.Glyphs.Clear();
        Fx.Reload<InspectorPanel>(page, editor);
        Assert.Equal("Font", page.Find(".side-head-name").Text);
    }
}

public sealed class GlyphPanelTests
{
    [Fact]
    public void BlankGlyphShowsItsCardAndState()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<GlyphPanel>());

        Assert.Equal("Character", page.Find(".panel-title").Text);
        Assert.Equal("Blank", page.Find(".panel-badge").Text);
        Assert.Equal("Capital A", page.Find(".glyph-card-name").Text);
        Assert.Contains("U+0041", page.Find(".glyph-card-code").Text);
        Assert.Contains("Uppercase", page.Find(".glyph-card-code").Text);
        Assert.Contains("Still blank", page.Find(".glyph-card-state").InnerText);
        Assert.Equal(3, page.FindAll(".num-field").Count);
        Assert.Equal("—", page.Find(".readout-value").Text);
    }

    [Fact]
    public void DrawnGlyphShowsDrawnStateAndInkWidth()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<GlyphPanel>());

        Assert.Equal("Drawn", page.Find(".panel-badge").Text);
        Assert.Contains("Drawn", page.Find(".glyph-card-state").InnerText);
        Assert.Equal("400", page.Find(".readout-value").Text);
    }

    [Fact]
    public void NumericFieldsChangeAdvanceAndSidebearings()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<GlyphPanel>());

        page.Change(page.FindAll(".num-field")[0], "720");
        Assert.Equal(720, glyph.AdvanceWidth);

        page.Change(page.FindAll(".num-field")[1], "50");
        Assert.Equal(50, glyph.LeftSideBearing);

        page.Change(page.FindAll(".num-field")[2], "30");
        Assert.Equal(480, glyph.AdvanceWidth);
    }

    [Fact]
    public void PointDetailsAppearForASelectedNode()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 0);
        var page = Fx.Render(editor, C<GlyphPanel>());

        Assert.Contains("Selected point", page.FindAll(".field-label").Select(label => label.Text));
        Assert.Equal(5, page.FindAll(".num-field").Count);

        page.Change(page.FindAll(".num-field")[3], "250");
        Assert.Equal(250f, glyph.Contours[0].Nodes[0].Point.X);

        page.Change(page.FindAll(".num-field")[4], "50");
        Assert.Equal(50f, glyph.Contours[0].Nodes[0].Point.Y);
    }

    [Fact]
    public void SmoothAndCornerChoicesChangeTheNode()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 1);
        var page = Fx.Render(editor, C<GlyphPanel>());

        page.FindAll(".choice").First(choice => choice.Text == "Smooth").Click();
        Assert.True(glyph.Contours[0].Nodes[1].Smooth);

        Fx.Reload<GlyphPanel>(page, editor);
        page.FindAll(".choice").First(choice => choice.Text == "Corner").Click();
        Assert.False(glyph.Contours[0].Nodes[1].Smooth);
    }

    [Fact]
    public void RetractButtonRemovesTheOutgoingHandle()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 1);
        editor.ToggleNodeSmooth();
        var page = Fx.Render(editor, C<GlyphPanel>());

        page.Click("[title='Retract the outgoing handle']");

        Assert.False(glyph.Contours[0].Nodes[1].HasOut);
    }

    [Fact]
    public void QuickActionButtonsRunTheirCommands()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 30, 300, 600, advance: 600);
        var page = Fx.Render(editor, C<GlyphPanel>());

        page.Click("[title='Flip horizontally']");
        Assert.Equal(300f, glyph.OutlineBounds.MinX);

        page.Click("[title='Centre between sidebearings']");
        Assert.Equal(200f, glyph.OutlineBounds.MinX);

        page.Click("[title='Duplicate to another character']");
        Assert.True(editor.ShowDuplicate);
        editor.CloseDialog();

        page.Click("[title='Clear the character']");
        Assert.False(glyph.HasOutline);
    }

    [Fact]
    public void VerticalFlipWorksFromThePanel()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 100, 500, 600);
        var page = Fx.Render(editor, C<GlyphPanel>());

        page.Click("[title='Flip vertically']");

        Assert.Equal(-100f, glyph.OutlineBounds.MinY);
    }

    [Fact]
    public void BlankGlyphHidesNodeDetailActions()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<GlyphPanel>());

        Assert.DoesNotContain("Selected point", page.FindAll(".field-label").Select(label => label.Text));
        Assert.Empty(page.FindAll(".mini-btn[title='Retract the outgoing handle']"));
    }
}

public sealed class MetricsPanelTests
{
    [Fact]
    public void MetricsPanelStartsClosedAndShowsTheUpmBadge()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<MetricsPanel>());

        Assert.Equal("Metrics", page.Find(".panel-title").Text);
        Assert.Equal("1000 upm", page.Find(".panel-badge").Text);
        Assert.Null(page.TryFind(".panel-body"));
    }

    [Fact]
    public void ExpandedMetricsExposeEveryGuide()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<MetricsPanel>());
        page.Click(".panel-head");

        Assert.Equal(6, page.FindAll(".field-row").Count);
        Assert.Equal(6, page.FindAll(".num-field").Count);
        Assert.Equal(["UPM", "Asc", "Desc", "Cap", "x", "Gap"], page.FindAll(".field-key").Select(key => key.Text));
        Assert.NotNull(page.TryFind(".panel-hint"));
    }

    [Fact]
    public void MetricFieldsUpdateTheModel()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<MetricsPanel>());
        page.Click(".panel-head");

        page.Change(page.FindAll(".num-field")[1], "850");
        Assert.Equal(850, editor.Style.Metrics.Ascender);

        page.Change(page.FindAll(".num-field")[2], "-250");
        Assert.Equal(-250, editor.Style.Metrics.Descender);

        page.Change(page.FindAll(".num-field")[3], "720");
        Assert.Equal(720, editor.Style.Metrics.CapHeight);

        page.Change(page.FindAll(".num-field")[4], "520");
        Assert.Equal(520, editor.Style.Metrics.XHeight);

        page.Change(page.FindAll(".num-field")[5], "50");
        Assert.Equal(50, editor.Style.Metrics.LineGap);
    }

    [Fact]
    public void UpmFieldRescalesTheWholeStyle()
    {
        FontEditor editor = Fx.Sample();
        var page = Fx.Render(editor, C<MetricsPanel>());
        page.Click(".panel-head");

        page.Change(page.FindAll(".num-field")[0], "2000");

        Assert.Equal(2000, editor.Style.Metrics.UnitsPerEm);
        Assert.Equal(1600, editor.Style.Metrics.Ascender);
        Assert.Equal(1400, editor.Style.Metrics.CapHeight);
    }

    [Fact]
    public void ResetGuidesAndGuideToggleButtonsWork()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Metrics.Ascender = 1234;
        editor.Style.Metrics.CapHeight = 12;
        var page = Fx.Render(editor, C<MetricsPanel>());
        page.Click(".panel-head");

        page.FindAll(".btn-tiny").First(button => button.Text == "Reset guides").Click();

        Assert.Equal(800, editor.Style.Metrics.Ascender);
        Assert.Equal(-200, editor.Style.Metrics.Descender);
        Assert.Equal(700, editor.Style.Metrics.CapHeight);
        Assert.Equal(500, editor.Style.Metrics.XHeight);
        Assert.Equal(0, editor.Style.Metrics.LineGap);

        TestElement guides = page.FindAll(".btn-tiny").First(button => button.InnerText == "Guides on");
        guides.Click();
        Assert.False(editor.ShowMetrics);
    }
}

public sealed class KerningPanelTests
{
    [Fact]
    public void KerningPanelShowsNoneAndStartsClosed()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());

        Assert.Equal("Kerning", page.Find(".panel-title").Text);
        Assert.Equal("None", page.Find(".panel-badge").Text);
        Assert.Null(page.TryFind(".panel-body"));
    }

    [Fact]
    public void ExpandedKerningPanelShowsControlsChipsAndAddForm()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        Assert.NotNull(page.TryFind(".kern-canvas"));
        Assert.Equal("AV", page.Find(".kern-pair-label").Text);
        Assert.Equal("0 units", page.Find(".kern-pair-value").Text);
        Assert.Equal(8, page.FindAll(".kern-chip").Count);
        Assert.Equal(2, page.FindAll(".kern-char").Count);
        Assert.Single(page.FindAll(".kern-controls .kern-field"));
        Assert.Empty(page.FindAll(".kern-list"));
    }

    [Fact]
    public void NudgingChangesTheCurrentPair()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        page.Click("[title='Tighten by 5 units']");
        Assert.Equal(5, editor.CurrentKern);

        page.Click("[title='Loosen by 5 units']");
        Assert.Equal(0, editor.CurrentKern);

        page.Click("[title='Tighten by 5 units']");
        page.Click("[title='Tighten by 5 units']");
        Assert.Equal(10, editor.CurrentKern);
    }

    [Fact]
    public void TypingAValueSetsThePairAndRemovingClearsIt()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        page.Change(".kern-controls .kern-field", "-70");
        Assert.Equal(-70, editor.Style.Kern('A', 'V'));

        page.Click("[title='Remove this pair']");
        Assert.Equal(0, editor.Style.Kern('A', 'V'));
    }

    [Fact]
    public void SuggestionChipsChooseThePair()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        page.FindAll(".kern-chip").First(chip => chip.Text == "To").Click();

        Assert.Equal('T', editor.KerningLeft);
        Assert.Equal('o', editor.KerningRight);
    }

    [Fact]
    public void AddPairReadsBothCharactersAndTheValue()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        page.Change(page.FindAll(".kern-char")[0], "L");
        page.Change(page.FindAll(".kern-char")[1], "T");
        page.Change(page.FindAll(".kern-add .kern-field")[0], "-80");
        page.FindAll(".btn-tiny").First(button => button.Text == "Add").Click();

        Assert.Equal(-80, editor.Style.Kern('L', 'T'));
        Assert.Contains("LT", editor.Status);
    }

    [Fact]
    public void ExistingPairsListSelectAndRemoveRows()
    {
        FontEditor editor = Fx.Editor();
        editor.SetKern('A', 'V', -40);
        editor.SetKern('T', 'o', -90);
        var page = Fx.Render(editor, C<KerningPanel>());
        page.Click(".panel-head");

        Assert.Equal("2 pairs", page.Find(".panel-badge").Text);
        Assert.Equal(2, page.FindAll(".kern-row").Count);

        page.FindAll(".kern-row").First(row => row.Find(".kern-row-pair").Text == "To").Click();
        Assert.Equal('T', editor.KerningLeft);

        TestElement av = page.FindAll(".kern-row").First(row => row.Find(".kern-row-pair").Text == "AV");
        av.Find(".kern-row-remove").Click();

        Assert.Equal(0, editor.Style.Kern('A', 'V'));
        Assert.Equal(-90, editor.Style.Kern('T', 'o'));
    }
}

public sealed class FontPanelTests
{
    [Fact]
    public void FontPanelShowsFamilyMetadataAndStyleChoices()
    {
        FontEditor editor = Fx.Sample();
        var page = Fx.Render(editor, C<FontPanel>());

        Assert.Equal("Font", page.Find(".panel-title").Text);
        Assert.Equal("Occam", page.Find(".panel-badge").Text);
        Assert.Equal(4, page.FindAll(".choice").Count(choice => choice.Text is "Light" or "Regular" or "Medium" or "Bold"));
        Assert.Equal(3, page.FindAll(".choice").Count(choice => choice.Text is "Condensed" or "Normal" or "Expanded"));
        Assert.True(page.FindAll(".choice").First(choice => choice.Text == "Regular").HasClass("is-active"));
        Assert.True(page.FindAll(".choice").First(choice => choice.Text == "Normal").HasClass("is-active"));
    }

    [Fact]
    public void WeightAndWidthChoicesUpdateTheStyle()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<FontPanel>());

        page.FindAll(".choice").First(choice => choice.Text == "Bold").Click();
        Assert.Equal(700, editor.Style.Weight);

        page.FindAll(".choice").First(choice => choice.Text == "Condensed").Click();
        Assert.Equal(3, editor.Style.WidthClass);

        page.FindAll(".choice").First(choice => choice.Text == "Expanded").Click();
        Assert.Equal(7, editor.Style.WidthClass);
    }

    [Fact]
    public void NumericWeightAndWidthFieldsClamp()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<FontPanel>());

        page.Change(page.FindAll(".num-field")[0], "950");
        Assert.Equal(900, editor.Style.Weight);

        page.Change(page.FindAll(".num-field")[1], "99");
        Assert.Equal(9, editor.Style.WidthClass);
    }

    [Fact]
    public void ItalicToggleFlipsTheStyleFlag()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<FontPanel>());

        page.FindAll(".btn-tiny").First(button => button.Text == "Italic").Click();
        Assert.True(editor.Style.Italic);

        Fx.Reload<FontPanel>(page, editor);
        Assert.True(page.FindAll(".btn-tiny").First(button => button.Text == "Italic").HasClass("is-on"));
    }

    [Fact]
    public void MetadataFieldsCommitEveryFamilyValue()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<FontPanel>());

        TestElement Field(string label) => page.FindAll(".field-stack")
            .First(stack => stack.Find(".field-label").Text == label);

        page.Change(Field("Family name").Find(".text-field-wide"), "Family Two");
        page.Change(Field("Style name").Find(".text-field-wide"), "Book");
        page.Change(Field("Version").Find(".text-field-wide"), "2.000");
        page.Change(Field("Designer").Find(".text-field-wide"), "Designer");
        page.Change(Field("Description").Find(".text-area"), "A description");
        page.Change(Field("Copyright").Find(".text-area"), "Copyright");
        page.Change(Field("Licence").Find(".text-field-wide"), "Licence");

        Assert.Equal("Family Two", editor.Project.FamilyName);
        Assert.Equal("Book", editor.Style.Name);
        Assert.Equal("2.000", editor.Project.Version);
        Assert.Equal("Designer", editor.Project.Designer);
        Assert.Equal("A description", editor.Project.Description);
        Assert.Equal("Copyright", editor.Project.Copyright);
        Assert.Equal("Licence", editor.Project.License);
    }

    [Fact]
    public void ManageStylesButtonOpensTheDialog()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<FontPanel>());

        page.FindAll(".btn-tiny").First(button => button.InnerText.Contains("Manage styles")).Click();

        Assert.True(editor.ShowStyles);
    }
}
