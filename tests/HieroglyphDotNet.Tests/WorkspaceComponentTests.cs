using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public void WorkspaceShowsTheGlyphBarStatsAndToolRail()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        Assert.Equal("A", page.Find(".glyphbar-char").Text);
        Assert.Equal("Capital A", page.Find(".glyphbar-label").Text);
        Assert.Equal("U+0041", page.Find(".glyphbar-code").Text);
        Assert.Contains("Nothing drawn yet", page.Find(".glyphbar-meta").InnerText);
        Assert.Contains("x-height 500", page.Find(".glyphbar-meta").InnerText);
        Assert.Equal(10, page.FindAll(".tool-btn").Count);
        Assert.Equal(3, page.FindAll(".rail-sep").Count);
        Assert.Equal(4, page.FindAll(".stat").Count);
        Assert.Equal("640", page.FindAll(".stat-value")[0].Text);
        Assert.Contains("Choose a starter shape", page.Find(".legend-item").Text);
        Assert.NotNull(page.TryFind(".canvas-overlay"));
        Assert.Null(page.TryFind(".brush-bar"));
    }

    [Fact]
    public void WorkspaceToolButtonsChooseToolsAndMarkTheActiveOne()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        Assert.True(page.Find("[title='Node (A)']").HasClass("is-active"));

        page.Click("[title='Pen (P)']");
        Assert.Equal(GlyphTool.Pen, editor.Tool);

        Fx.Reload<Workspace>(page, editor);
        Assert.True(page.Find("[title='Pen (P)']").HasClass("is-active"));
        Assert.Contains("Click to place points", page.Find(".legend-item").Text);

        page.Click("[title='Brush (B)']");
        Assert.Equal(GlyphTool.Brush, editor.Tool);
        Fx.Reload<Workspace>(page, editor);
        Assert.NotNull(page.TryFind(".brush-bar"));
        Assert.Contains("Drag to paint", page.Find(".legend-item").Text);
    }

    [Fact]
    public void BrushBarAdjustsTheStrokeWidth()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);
        var page = Fx.Render(editor, C<Workspace>());

        Assert.Equal("78", page.Find(".brush-bar-field").Value);
        page.Click("[title='Thinner stroke']");
        Assert.Equal(68f, editor.BrushWidth);
        page.Click("[title='Thicker stroke']");
        Assert.Equal(78f, editor.BrushWidth);

        page.Change(".brush-bar-field", "120");
        Assert.Equal(120f, editor.BrushWidth);
    }

    [Fact]
    public void ViewButtonsToggleGuidesAndFit()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        page.Click("[title='Show the design grid']");
        Assert.False(editor.ShowGrid);

        page.Click("[title='Show metric guides']");
        Assert.False(editor.ShowMetrics);

        page.Click("[title='Show sidebearing guides']");
        Assert.False(editor.ShowBearings);

        editor.PanBy(-800, -800);
        page.Click("[title='Fit the character to the canvas (Ctrl+0)']");
        Assert.NotEqual(-800f, editor.PanX);
    }

    [Fact]
    public void StarterCardOffersBrushPenAndStarterShapes()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        Assert.Contains("A is waiting", page.Find(".starter-title").Text);
        Assert.Equal(6, page.FindAll(".starter-button").Count);

        page.FindAll(".starter-button").First(button => button.InnerText.Contains("Ellipse")).Click();
        Assert.Equal(2, editor.SelectedGlyph!.Contours.Count);
        Assert.False(editor.ShowStarterPanel);

        Fx.Reload<Workspace>(page, editor);
        Assert.Null(page.TryFind(".canvas-overlay"));
    }

    [Fact]
    public void StarterCardButtonsChooseTheBrushAndPen()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        page.Find("[title='Paint the character with the freehand brush']").Click();
        Assert.Equal(GlyphTool.Brush, editor.Tool);

        editor.SetTool(GlyphTool.Node);
        Fx.Reload<Workspace>(page, editor);
        page.Find("[title='Place points and pull curves']").Click();
        Assert.Equal(GlyphTool.Pen, editor.Tool);
    }

    [Fact]
    public void StarterRectangleStemAndCopyButtonsWork()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        page.FindAll(".starter-button").First(button => button.InnerText.Contains("Rectangle")).Click();
        Assert.Equal(2, editor.SelectedGlyph!.Contours.Count);

        editor.SelectGlyph('B');
        Fx.Reload<Workspace>(page, editor);
        page.FindAll(".starter-button").First(button => button.InnerText.Contains("Stem")).Click();
        Assert.Single(editor.SelectedGlyph!.Contours);

        Fx.DrawRect(editor, 'C', 100, 0, 500, 700);
        Fx.Reload<Workspace>(page, editor);
        Assert.Null(page.TryFind(".starter-card"));
    }

    [Fact]
    public void StarterCopyButtonExplainsThatACharacterIsNeeded()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Workspace>());

        page.FindAll(".starter-button").First(button => button.InnerText.Contains("Copy a character")).Click();

        Assert.False(editor.ShowDuplicate);
        Assert.Contains("Draw a character", editor.Status);
    }

    [Fact]
    public void GlyphBarActionsDuplicateClearAndCopyTemplates()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<Workspace>());

        page.Click("[title='Duplicate this character to another one (Ctrl+D)']");
        Assert.True(editor.ShowDuplicate);
        editor.CloseDialog();

        page.Click("[title='Copy a template from another character']");
        Assert.True(editor.ShowDuplicate);
        editor.CloseDialog();

        page.Click("[title='Clear this character']");
        Assert.False(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void GlyphBarShowsContourCountsAndSidebearings()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700, advance: 650);
        var page = Fx.Render(editor, C<Workspace>());

        Assert.Contains("1 contour", page.Find(".glyphbar-meta").InnerText);
        Assert.Contains("4 points", page.Find(".glyphbar-meta").InnerText);
        Assert.Equal("650", page.FindAll(".stat-value")[0].Text);
        Assert.Equal("100", page.FindAll(".stat-value")[1].Text);
        Assert.Equal("150", page.FindAll(".stat-value")[2].Text);
        Assert.Equal("400", page.FindAll(".stat-value")[3].Text);
    }

    [Fact]
    public void WorkspaceWithoutASelectedGlyphShowsAFallback()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Glyphs.Clear();
        var page = Fx.Render(editor, C<Workspace>());

        Assert.Equal("?", page.Find(".glyphbar-char").Text);
        Assert.Equal("No character", page.Find(".glyphbar-label").Text);
    }
}

public sealed class GlyphCanvasTests
{
    [Theory]
    [InlineData(GlyphTool.Select, "is-select")]
    [InlineData(GlyphTool.Node, "is-node")]
    [InlineData(GlyphTool.Pen, "is-pen")]
    [InlineData(GlyphTool.Brush, "is-select")]
    [InlineData(GlyphTool.Hand, "is-hand")]
    [InlineData(GlyphTool.Zoom, "is-zoom")]
    public void SurfaceUsesTheToolCursorClass(GlyphTool tool, string expected)
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(tool);
        var page = Fx.Render(editor, C<GlyphCanvas>());

        TestElement surface = page.Find(".surface");

        Assert.True(surface.HasClass(expected));
        Assert.Equal("1100", surface.GetAttribute("width"));
        Assert.Equal("760", surface.GetAttribute("height"));
    }

    [Fact]
    public void CanvasHasAStableSurfaceAcrossRenders()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<GlyphCanvas>());

        Assert.True(page.Find(".surface").HasClass("is-node"));

        editor.SetTool(GlyphTool.Hand);
        Fx.Reload<GlyphCanvas>(page, editor);

        Assert.True(page.Find(".surface").HasClass("is-hand"));
        Assert.False(page.Find(".surface").HasClass("is-node"));
    }
}

public sealed class CanvasPanTests
{
    [Fact]
    public void MiddleButtonDragPansTheCanvas()
    {
        InputControl.Reset();
        try
        {
            FontEditor editor = Fx.Editor();
            float panX = editor.PanX;
            float panY = editor.PanY;

            InputControl.PressSequence(1);
            InputControl.Set("Middle", true);
            InputControl.Pointer(400, 300);
            Assert.True(CanvasPan.Handle(editor));

            InputControl.Pointer(450, 350);
            Assert.True(CanvasPan.Handle(editor));
            Assert.Equal(panX + 50, editor.PanX);
            Assert.Equal(panY + 50, editor.PanY);

            InputControl.Set("Middle", false);
            Assert.False(CanvasPan.Handle(editor));
        }
        finally
        {
            InputControl.Reset();
            CanvasPan.Reset();
        }
    }

    [Fact]
    public void ReleasingTheMiddleButtonEndsThePan()
    {
        InputControl.Reset();
        try
        {
            FontEditor editor = Fx.Editor();
            InputControl.PressSequence(5);
            InputControl.Set("Middle", true);
            InputControl.Pointer(100, 100);
            CanvasPan.Handle(editor);

            InputControl.Set("Middle", false);
            CanvasPan.Handle(editor);

            InputControl.Set("Middle", true);
            InputControl.Pointer(200, 200);
            Assert.True(CanvasPan.Handle(editor));
            InputControl.Pointer(250, 250);
            CanvasPan.Handle(editor);
            Assert.Equal(editor.PanX, editor.PanX);
        }
        finally
        {
            InputControl.Reset();
            CanvasPan.Reset();
        }
    }

    [Fact]
    public void ResetStopsPanningWithoutMoving()
    {
        InputControl.Reset();
        try
        {
            FontEditor editor = Fx.Editor();
            InputControl.PressSequence(9);
            InputControl.Set("Middle", true);
            InputControl.Pointer(10, 10);
            CanvasPan.Handle(editor);
            float panX = editor.PanX;

            CanvasPan.Reset();
            InputControl.Pointer(90, 90);
            float after = editor.PanX;

            Assert.True(CanvasPan.Handle(editor));
            Assert.Equal(after, editor.PanX);
        }
        finally
        {
            InputControl.Reset();
            CanvasPan.Reset();
        }
    }
}
