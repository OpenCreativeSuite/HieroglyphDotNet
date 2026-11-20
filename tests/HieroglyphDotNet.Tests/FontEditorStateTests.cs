using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class FontEditorStateTests
{
    [Fact]
    public void ConstructorLoadsTheStarterTypeface()
    {
        FontEditor editor = new();

        Assert.Equal("Occam", editor.Project.FamilyName);
        Assert.Single(editor.Project.Styles);
        Assert.Equal("Regular", editor.Style.Name);
        Assert.Equal(1000, editor.Style.Metrics.UnitsPerEm);
        Assert.True(editor.Project.DrawnCount > 60);
        Assert.Equal('A', editor.SelectedCode);
        Assert.Equal('A', editor.SelectedGlyph!.Codepoint);
        Assert.Equal(GlyphTool.Node, editor.Tool);
        Assert.Equal(1.35f, editor.PreviewLeading);
        Assert.Equal("Handgloves", editor.PreviewText);
        Assert.Contains("Starter typeface loaded", editor.Status);
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.True(editor.ShowGrid);
        Assert.True(editor.ShowMetrics);
        Assert.True(editor.ShowBearings);
        Assert.True(editor.ShowBrowser);
        Assert.True(editor.ShowInspector);
        Assert.True(editor.ShowPreview);
        Assert.False(editor.IsPenActive);
        Assert.False(editor.IsBrushActive);
        Assert.False(editor.IsDragging);
        Assert.Null(editor.Marquee);
        Assert.Null(editor.PenPreview);
        Assert.Null(editor.BrushPreview);
        Assert.Empty(editor.BrushPoints);
    }

    [Fact]
    public void NotifyInvokesTheRefreshCallback()
    {
        FontEditor editor = Fx.Editor();
        int refreshes = 0;
        editor.Refresh = () => refreshes++;

        editor.Notify();
        editor.NotifyStatus("Hello");

        Assert.Equal(2, refreshes);
        Assert.Equal("Hello", editor.Status);
    }

    [Fact]
    public void TouchBumpsGeometryAndMarksDirtyWhileMarkDirtyLeavesGeometry()
    {
        FontEditor editor = Fx.Editor();
        int version = editor.GeometryVersion;

        editor.MarkDirty();
        Assert.True(editor.Project.Dirty);
        Assert.Equal(version, editor.GeometryVersion);

        editor.Touch();
        Assert.Equal(version + 1, editor.GeometryVersion);
    }

    [Fact]
    public void StaticNumberAndParseHelpersHandleUserInput()
    {
        Assert.Equal("2.5", FontEditor.Number(2.5f));
        Assert.Equal("3", FontEditor.Whole(2.6f));
        Assert.Equal(2.5f, FontEditor.Parse(" 2,5 ", 0f));
        Assert.Equal(7f, FontEditor.Parse("not a number", 7f));
        Assert.Equal(3, FontEditor.ParseInt("3.2", 0));
        Assert.Equal(4, FontEditor.ParseInt("", 4));
        Assert.Equal("2.5", FontEditor.Number(FontEditor.Parse("2.50", 0f)));
    }

    [Fact]
    public void SelectGlyphAddsMissingCharactersAndResetsEditing()
    {
        FontEditor editor = Fx.Editor();
        editor.SelectNode(0, 0);

        editor.SelectGlyph(0x2603);

        Assert.Equal(0x2603, editor.SelectedCode);
        Assert.Equal(0x2603, editor.SelectedGlyph!.Codepoint);
        Assert.Equal("U+2603", editor.SelectedGlyph.Name);
        Assert.Equal(-1, editor.SelectedContour);
        Assert.Empty(editor.SelectedNodes);
        Assert.True(editor.ShowStarterPanel);
        Assert.Contains("2603", editor.Status);
    }

    [Fact]
    public void SelectingTheSameGlyphKeepsNodeSelection()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 1);
        string status = editor.Status;

        editor.SelectGlyph('A');

        Assert.Equal(0, editor.SelectedContour);
        Assert.Equal(1, editor.SelectedNode);
        Assert.Equal(status, editor.Status);
    }

    [Fact]
    public void SelectGlyphCancelsPenAndBrushGestures()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 0));
        Assert.True(editor.IsPenActive);

        editor.SetTool(GlyphTool.Brush);
        editor.PointerDown(editor.View.ToScreen(new Vec2(200, 100)), false, false, middle: false);
        Assert.True(editor.IsBrushActive);

        editor.SelectGlyph('B');

        Assert.False(editor.IsPenActive);
        Assert.False(editor.IsBrushActive);
        Assert.Empty(editor.BrushPoints);
    }

    [Fact]
    public void StepGlyphWrapsAroundTheFilteredList()
    {
        FontEditor editor = Fx.Editor();
        editor.SelectGlyph('A');

        editor.StepGlyph(-1);
        Assert.Equal(0x2665, editor.SelectedCode);

        editor.StepGlyph(1);
        Assert.Equal('A', editor.SelectedCode);

        editor.BrowserCategory = CharacterCategory.Numbers;
        editor.SelectGlyph('9');
        editor.StepGlyph(1);
        Assert.Equal('0', editor.SelectedCode);
    }

    [Fact]
    public void SteppingAnEmptyFilteredListDoesNothing()
    {
        FontEditor editor = Fx.Editor();
        editor.BrowserSearch = "no-such-character";
        editor.SelectGlyph('A');

        editor.StepGlyph(1);

        Assert.Equal('A', editor.SelectedCode);
    }

    [Fact]
    public void FilteredCharactersApplyCategoryBlankAndSearchFilters()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.Equal(editor.Style.Glyphs.Count, editor.FilteredCharacters().Count);

        editor.BrowserCategory = CharacterCategory.Uppercase;
        Assert.All(editor.FilteredCharacters(), glyph => Assert.Equal(CharacterCategory.Uppercase, CharacterSet.CategoryFor(glyph.Codepoint)));

        editor.BrowserCategory = null;
        editor.BrowserOnlyBlank = true;
        Assert.DoesNotContain(editor.FilteredCharacters(), glyph => glyph.Codepoint == 'A');
        Assert.All(editor.FilteredCharacters(), glyph => Assert.False(glyph.HasOutline));

        editor.BrowserOnlyBlank = false;
        editor.BrowserSearch = "Capital B";
        Assert.Equal('B', Assert.Single(editor.FilteredCharacters()).Codepoint);

        editor.BrowserSearch = "0042";
        Assert.Equal('B', Assert.Single(editor.FilteredCharacters()).Codepoint);

        editor.BrowserSearch = "  b  ";
        Assert.Contains(editor.FilteredCharacters(), glyph => glyph.Codepoint == 'B');
        Assert.Contains(editor.FilteredCharacters(), glyph => glyph.Codepoint == 'b');
    }

    [Fact]
    public void FilteredCharactersAreOrderedByCategoryThenCode()
    {
        FontEditor editor = Fx.Editor();
        List<Glyph> characters = editor.FilteredCharacters();

        Assert.Equal('A', characters[0].Codepoint);
        Assert.Equal('Z', characters[25].Codepoint);
        Assert.Equal('a', characters[26].Codepoint);
        Assert.Equal('0', characters[52].Codepoint);
        Assert.Equal(0x20, characters[62].Codepoint);
    }

    [Fact]
    public void CategoryProgressCountsDrawnAndTotal()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.Equal((1, 26), editor.CategoryProgress(CharacterCategory.Uppercase));
        Assert.Equal((0, 26), editor.CategoryProgress(CharacterCategory.Lowercase));
        Assert.Equal((0, 0), editor.CategoryProgress(CharacterCategory.Custom));

        editor.Project.EnsureCharacter(0x2603);
        Assert.Equal((0, 1), editor.CategoryProgress(CharacterCategory.Custom));
    }

    [Fact]
    public void SelectionBoundsAndLabelDescribeWholeGlyphOrNode()
    {
        FontEditor editor = Fx.Editor();
        Assert.Equal("Empty character", editor.SelectionLabel);

        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        Assert.Equal("1 contour · 4 points", editor.SelectionLabel);
        Assert.Equal(new Bounds(100, 0, 500, 700), editor.SelectionBounds);

        editor.SelectNode(0, 2);
        Assert.Equal("Corner node  500, 700", editor.SelectionLabel);
        Assert.Equal(new Bounds(500, 700, 500, 700), editor.SelectionBounds);

        glyph.Contours[0].Nodes[2].Smooth = true;
        glyph.Contours[0].Nodes[2].HasOut = true;
        Assert.StartsWith("Smooth node", editor.SelectionLabel);

        glyph.Contours[0].Nodes[2].Smooth = false;
        Assert.StartsWith("Curve node", editor.SelectionLabel);

        editor.ClearSelection();
        Assert.Equal("1 contour · 4 points", editor.SelectionLabel);
    }

    [Fact]
    public void SelectionBoundsAreSafeForOutOfRangeNodes()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(8, 9);

        Assert.Equal(glyph.OutlineBounds, editor.SelectionBounds);
        Assert.Equal("Node", editor.SelectionLabel);
    }

    [Fact]
    public void SetViewSizePerformsOneInitialFitAndIgnoresTinySizes()
    {
        FontEditor editor = new();
        float zoom = editor.Zoom;

        editor.SetViewSize(4, 4);
        Assert.Equal(zoom, editor.Zoom);

        editor.SetViewSize(1200, 800);
        Assert.Equal(1200f, editor.ViewWidth);
        Assert.Equal(800f, editor.ViewHeight);
        Assert.NotEqual(zoom, editor.Zoom);

        editor.PanBy(20, 10);
        float fitted = editor.Zoom;
        editor.SetViewSize(400, 300);
        Assert.Equal(400f, editor.ViewWidth);
        Assert.Equal(fitted, editor.Zoom);
    }

    [Fact]
    public void FitGlyphScalesToTheCanvasAndFramesTheAdvance()
    {
        FontEditor editor = new();
        editor.SelectGlyph('W');
        editor.SetViewSize(1000, 700);

        editor.FitGlyph();

        Assert.InRange(editor.Zoom, 0.02f, 12f);
        Assert.True(editor.PanX > 0f);
        Vec2 ascender = editor.View.ToScreen(0, editor.Style.Metrics.Ascender);
        Vec2 baseline = editor.View.ToScreen(0, 0);
        Vec2 descender = editor.View.ToScreen(0, editor.Style.Metrics.Descender);
        Assert.InRange(baseline.Y, 0f, 700f);
        Assert.True(ascender.Y < baseline.Y);
        Assert.True(descender.Y > baseline.Y);
        Assert.InRange(descender.Y, 0f, 700f);
    }

    [Fact]
    public void ZoomAtKeepsThePointUnderTheCursorStable()
    {
        FontEditor editor = Fx.Editor();
        Vec2 point = new(320, 240);
        Vec2 before = editor.View.ToUnits(point);

        editor.ZoomAt(1.5f, point);

        Vec2 after = editor.View.ToUnits(point);
        Fx.Nearly(before.X, after.X);
        Fx.Nearly(before.Y, after.Y);

        float zoom = editor.Zoom;
        editor.ZoomAt(1000f, point);
        Assert.Equal(16f, editor.Zoom);
        editor.ZoomAt(0.0001f, point);
        Assert.Equal(0.02f, editor.Zoom);
        Assert.True(zoom > 0f);
    }

    [Fact]
    public void ZoomAtDoesNothingAtTheLimit()
    {
        FontEditor editor = Fx.Editor();
        editor.ZoomAt(16f / editor.Zoom, Vec2.Zero);
        float panX = editor.PanX;

        editor.ZoomAt(2f, new Vec2(100, 100));

        Assert.Equal(16f, editor.Zoom);
        Assert.Equal(panX, editor.PanX);
    }

    [Fact]
    public void ZoomStepWalksThePresetLadderInBothDirections()
    {
        FontEditor editor = Fx.Editor();
        editor.ZoomAt(0.5f / editor.Zoom, Vec2.Zero);

        editor.ZoomStep(1);
        Assert.Equal(0.65f, editor.Zoom, 3);

        editor.ZoomStep(-1);
        Assert.Equal(0.5f, editor.Zoom, 3);

        editor.ZoomAt(0.01f, Vec2.Zero);
        editor.ZoomStep(-1);
        Assert.Equal(0.02f, editor.Zoom, 3);
    }

    [Fact]
    public void PanAndCursorUpdateTheSession()
    {
        FontEditor editor = Fx.Editor();
        float panX = editor.PanX;
        float panY = editor.PanY;

        editor.PanBy(15, -25);

        Assert.Equal(panX + 15, editor.PanX);
        Assert.Equal(panY - 25, editor.PanY);

        editor.SetCursor(new Vec2(123, 456), onCanvas: true);
        Assert.Equal(new Vec2(123, 456), editor.CursorUnits);
        Assert.True(editor.CursorOnCanvas);

        editor.PointerLeave();
        Assert.False(editor.CursorOnCanvas);
    }

    [Fact]
    public void ToolNamesCoverEveryTool()
    {
        Assert.Equal("Selection", FontEditor.ToolName(GlyphTool.Select));
        Assert.Equal("Node", FontEditor.ToolName(GlyphTool.Node));
        Assert.Equal("Pen", FontEditor.ToolName(GlyphTool.Pen));
        Assert.Equal("Brush", FontEditor.ToolName(GlyphTool.Brush));
        Assert.Equal("Hand", FontEditor.ToolName(GlyphTool.Hand));
        Assert.Equal("Zoom", FontEditor.ToolName(GlyphTool.Zoom));
    }

    [Fact]
    public void SetToolSwitchesAndUpdatesStatus()
    {
        FontEditor editor = Fx.Editor();

        foreach (GlyphTool tool in Enum.GetValues<GlyphTool>())
        {
            editor.SetTool(tool);
            Assert.Equal(tool, editor.Tool);
            Assert.False(string.IsNullOrWhiteSpace(editor.Status));
        }

        editor.SelectGlyph('B');
        editor.SetTool(GlyphTool.Pen);
        editor.SetTool(GlyphTool.Brush);
        editor.SetTool(GlyphTool.Select);
        Assert.True(editor.ShowStarterPanel);
    }

    [Fact]
    public void SettingTheSameToolDoesNotNotify()
    {
        FontEditor editor = Fx.Editor();
        int refreshes = 0;
        editor.Refresh = () => refreshes++;

        editor.SetTool(editor.Tool);

        Assert.Equal(0, refreshes);
    }

    [Fact]
    public void BrushWidthRoundsAndClamps()
    {
        FontEditor editor = Fx.Editor();

        editor.SetBrushWidth(1);
        Assert.Equal(8f, editor.BrushWidth);
        editor.SetBrushWidth(1000);
        Assert.Equal(320f, editor.BrushWidth);
        editor.SetBrushWidth(100.6f);
        Assert.Equal(101f, editor.BrushWidth);
        Assert.Contains("101", editor.Status);
    }

    [Fact]
    public void ViewTogglesFlipEachFlag()
    {
        FontEditor editor = Fx.Editor();

        editor.ToggleGrid();
        Assert.False(editor.ShowGrid);
        editor.ToggleGrid();
        Assert.True(editor.ShowGrid);

        editor.ToggleMetrics();
        Assert.False(editor.ShowMetrics);
        editor.ToggleMetrics();
        Assert.True(editor.ShowMetrics);

        editor.ToggleBearings();
        Assert.False(editor.ShowBearings);
        editor.ToggleBearings();
        Assert.True(editor.ShowBearings);

        editor.ToggleBrowser();
        Assert.False(editor.ShowBrowser);
        editor.ToggleBrowser();
        Assert.True(editor.ShowBrowser);

        editor.ToggleInspector();
        Assert.False(editor.ShowInspector);
        editor.ToggleInspector();
        Assert.True(editor.ShowInspector);

        editor.TogglePreview();
        Assert.False(editor.ShowPreview);
        editor.TogglePreview();
        Assert.True(editor.ShowPreview);

        editor.ToggleStarterPanel();
        Assert.False(editor.ShowStarterPanel);
        editor.ToggleStarterPanel();
        Assert.True(editor.ShowStarterPanel);
    }

    [Fact]
    public void PreviewTabCanBeChanged()
    {
        FontEditor editor = Fx.Editor();

        editor.SetPreviewTab(PreviewTab.Alphabet);
        Assert.Equal(PreviewTab.Alphabet, editor.PreviewTab);
        editor.SetPreviewTab(PreviewTab.Kerning);
        Assert.Equal(PreviewTab.Kerning, editor.PreviewTab);
    }

    [Fact]
    public void UndoRestoresGlyphGeometryAndRedoReappliesIt()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.PushUndo();
        glyph.Contours.Clear();
        editor.Touch();
        Assert.False(glyph.HasOutline);
        Assert.True(editor.CanUndo);

        editor.Undo();
        Assert.True(editor.SelectedGlyph!.HasOutline);
        Assert.Equal(4, editor.SelectedGlyph.NodeCount);
        Assert.True(editor.CanRedo);

        editor.Redo();
        Assert.False(editor.SelectedGlyph!.HasOutline);
        Assert.True(editor.CanUndo);
    }

    [Fact]
    public void UndoRestoresMetricsAndKerning()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.PushUndo();
        editor.Style.Metrics.XHeight = 1;
        editor.Style.Kerning[('A', 'V')] = -99;
        editor.Touch();

        editor.Undo();

        Assert.Equal(500, editor.Style.Metrics.XHeight);
        Assert.Equal(0, editor.Style.Kern('A', 'V'));
    }

    [Fact]
    public void PushUndoClearsTheRedoStack()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.PushUndo();
        editor.ClearGlyph();
        editor.Undo();
        Assert.True(editor.CanRedo);

        editor.PushUndo();
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void UndoAndRedoAreNoOpsOnEmptyStacks()
    {
        FontEditor editor = Fx.Editor();
        string status = editor.Status;

        editor.Undo();
        editor.Redo();

        Assert.Equal(status, editor.Status);
    }

    [Fact]
    public void UndoHistoryIsCappedAtOneHundredAndTwentySnapshots()
    {
        FontEditor editor = Fx.Editor();
        for (int i = 0; i < 130; i++)
        {
            editor.PushUndo();
        }

        for (int i = 0; i < 120; i++)
        {
            Assert.True(editor.CanUndo);
            editor.Undo();
        }

        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void KerningUndoCapturesOnlyKerning()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.Style.Kerning[('A', 'V')] = -40;

        editor.PushKerningUndo();
        editor.Style.Kerning[('A', 'V')] = -90;
        editor.Undo();

        Assert.Equal(-40, editor.Style.Kern('A', 'V'));
        Assert.True(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void RasterCacheTracksTheGeometryVersion()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        GlyphBitmap first = editor.RasterCache.Get(glyph, 0.1f);
        Assert.Same(first, editor.RasterCache.Get(glyph, 0.1f));

        editor.Touch();
        GlyphBitmap second = editor.RasterCache.Get(glyph, 0.1f);
        Assert.NotSame(first, second);
        Assert.Same(editor.Raster, editor.RasterCache);
    }
}
