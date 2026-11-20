using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class FontEditorHitTestTests
{
    [Fact]
    public void DescribeHitFindsNodesContoursAdvanceAndEmptySpace()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.Equal((0, 0, 0), editor.DescribeHit(editor.View.ToScreen(glyph.Contours[0].Nodes[0].Point)));
        Assert.Equal((0, -1, 0), editor.DescribeHit(editor.View.ToScreen(100, 350)));
        Assert.Equal((-1, -1, 0), editor.DescribeHit(editor.View.ToScreen(glyph.AdvanceWidth, 300)));
        Assert.Equal((-1, -1, 0), editor.DescribeHit(editor.View.ToScreen(-400, -400)));
    }

    [Fact]
    public void DescribeHitFindsHandlesOnlyForTheSelectedNode()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        GlyphNode node = glyph.Contours[0].Nodes[0];
        node.HasOut = true;
        node.Out = new Vec2(150, 80);
        node.HasIn = true;
        node.In = new Vec2(50, -80);

        Assert.Equal((-1, -1, 0), editor.DescribeHit(editor.View.ToScreen(150, 80)));

        editor.SelectNode(0, 0);
        Assert.Equal((0, 0, 1), editor.DescribeHit(editor.View.ToScreen(150, 80)));
        Assert.Equal((0, 0, -1), editor.DescribeHit(editor.View.ToScreen(50, -80)));
    }

    [Fact]
    public void CurveSegmentsAreHitTestable()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);

        GlyphContour contour = glyph.Contours[0];
        contour.Nodes[0].HasOut = true;
        contour.Nodes[0].Out = new Vec2(200, 100);
        contour.Nodes[1].HasIn = true;
        contour.Nodes[1].In = new Vec2(400, 100);

        Assert.Equal((0, -1, 0), editor.DescribeHit(editor.View.ToScreen(300, 75)));
    }
}

public sealed class FontEditorPointerTests
{
    [Fact]
    public void NodeDragMovesTheSelectedPointIncludingHandles()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        GlyphNode node = Fx.SelectFirstNode(editor);
        node.HasOut = true;
        node.Out = new Vec2(150, 50);

        Fx.Drag(editor, node.Point, new Vec2(node.Point.X + 40, node.Point.Y + 30));

        Fx.Nearly(140f, node.Point.X);
        Fx.Nearly(30f, node.Point.Y);
        Fx.Nearly(190f, node.Out.X);
        Fx.Nearly(80f, node.Out.Y);
        Assert.True(editor.Project.Dirty);
        Assert.False(editor.IsDragging);
    }

    [Fact]
    public void ShiftDragConstrainsTheDominantAxis()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        GlyphNode node = Fx.SelectFirstNode(editor);

        Fx.Drag(editor, node.Point, new Vec2(node.Point.X + 50, node.Point.Y + 5), shift: true);
        Fx.Nearly(150f, node.Point.X);
        Fx.Nearly(0f, node.Point.Y);

        Fx.Drag(editor, node.Point, new Vec2(node.Point.X + 5, node.Point.Y + 50), shift: true);
        Fx.Nearly(150f, node.Point.X);
        Fx.Nearly(50f, node.Point.Y);
    }

    [Fact]
    public void SelectionToolSwitchesToNodeEditingWhenANodeIsClicked()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        Vec2 start = editor.View.ToScreen(glyph.Contours[0].Nodes[1].Point);

        editor.SetTool(GlyphTool.Select);
        editor.PointerDown(start, false, false, middle: false);
        editor.PointerUp(start);

        Assert.Equal(GlyphTool.Node, editor.Tool);
        Assert.Equal(1, editor.SelectedNode);
    }

    [Fact]
    public void SelectionToolDragsTheWholeShape()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        Bounds before = glyph.OutlineBounds;

        editor.SetTool(GlyphTool.Select);
        Fx.Drag(editor, new Vec2(100, 350), new Vec2(160, 390));

        Fx.Nearly(160f, glyph.OutlineBounds.MinX);
        Fx.Nearly(740f, glyph.OutlineBounds.MaxY);
    }

    [Fact]
    public void AdvanceHandleDragsTheAdvanceWidth()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700, advance: 600);
        editor.SetTool(GlyphTool.Select);

        Fx.Drag(editor, new Vec2(600, 300), new Vec2(720, 300));

        Assert.Equal(720, glyph.AdvanceWidth);
        Assert.True(editor.Project.Dirty);
    }

    [Fact]
    public void AdvanceDragClampsToTheSupportedRange()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700, advance: 600);
        editor.SetTool(GlyphTool.Select);

        Fx.Drag(editor, new Vec2(600, 300), new Vec2(-1000, 300));
        Assert.Equal(0, glyph.AdvanceWidth);

        Fx.Drag(editor, new Vec2(0, 300), new Vec2(9000, 300));
        Assert.Equal(4000, glyph.AdvanceWidth);
    }

    [Fact]
    public void MarqueeSelectsEveryNodeInsideTheBox()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);

        Fx.Drag(editor, new Vec2(50, -50), new Vec2(550, 750));

        Assert.Equal(0, editor.SelectedContour);
        Assert.Equal(4, editor.SelectedNodes.Count);
        Assert.Null(editor.Marquee);
    }

    [Fact]
    public void TinyMarqueeSelectsNothing()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        editor.SelectAllNodes();

        Fx.Drag(editor, new Vec2(-100, -100), new Vec2(-99, -99));

        Assert.Empty(editor.SelectedNodes);
    }

    [Fact]
    public void NodePressOnEmptySpaceStartsAMarquee()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        Vec2 start = editor.View.ToScreen(-200, -200);

        editor.PointerDown(start, false, false, middle: false);
        Assert.True(editor.IsDragging);
        Assert.NotNull(editor.Marquee);

        editor.PointerMove(editor.View.ToScreen(-100, -100), false);
        editor.PointerUp(editor.View.ToScreen(-100, -100));

        Assert.Empty(editor.SelectedNodes);
    }

    [Fact]
    public void ContourClickSelectsTheContour()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        Vec2 edge = editor.View.ToScreen(100, 350);

        editor.PointerDown(edge, false, false, middle: false);
        editor.PointerUp(edge);

        Assert.Equal(0, editor.SelectedContour);
        Assert.Empty(editor.SelectedNodes);
    }

    [Fact]
    public void ShiftClickOnAContourKeepsTheSelection()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        editor.SelectNode(0, 0);

        Vec2 edge = editor.View.ToScreen(100, 350);
        editor.PointerDown(edge, shift: true, alt: false, middle: false);
        editor.PointerUp(edge);

        Assert.Equal(0, editor.SelectedContour);
        Assert.Single(editor.SelectedNodes);
    }

    [Fact]
    public void HandleDragMirrorsSmoothNodesAndAltBreaksThem()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        GlyphNode node = Fx.SelectFirstNode(editor);
        node.Smooth = true;
        node.HasOut = true;
        node.Out = new Vec2(150, 50);

        Fx.Drag(editor, node.Out, new Vec2(200, 100));

        Fx.Nearly(200f, node.Out.X);
        Fx.Nearly(100f, node.Out.Y);
        Assert.True(node.HasIn);
        Fx.Nearly(0f, node.In.X);
        Fx.Nearly(-100f, node.In.Y);

        editor.SelectNode(0, 0);
        node.In = new Vec2(20, 20);
        node.HasIn = true;
        Fx.Drag(editor, node.Out, new Vec2(220, 120), alt: true);
        Assert.False(node.Smooth);
    }

    [Fact]
    public void IncomingHandleDragMirrorsSmoothNodes()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Node);
        GlyphNode node = Fx.SelectFirstNode(editor);
        node.Smooth = true;
        node.HasIn = true;
        node.In = new Vec2(50, -50);

        Fx.Drag(editor, node.In, new Vec2(20, -20));

        Fx.Nearly(20f, node.In.X);
        Fx.Nearly(-20f, node.In.Y);
        Fx.Nearly(180f, node.Out.X);
        Fx.Nearly(20f, node.Out.Y);
    }

    [Fact]
    public void HandToolPansInsteadOfEditing()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetTool(GlyphTool.Hand);
        float panX = editor.PanX;
        float panY = editor.PanY;

        Fx.Drag(editor, new Vec2(100, 300), new Vec2(200, 250));

        Assert.True(editor.PanX > panX);
        Assert.True(editor.PanY > panY, $"panY {panY} became {editor.PanY}");
        Assert.True(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void MiddleDragPansWithAnyTool()
    {
        FontEditor editor = Fx.Editor();
        Vec2 start = new(400, 300);
        Vec2 end = new(450, 350);
        float panX = editor.PanX;
        float panY = editor.PanY;

        editor.PointerDown(start, false, false, middle: true);
        editor.PointerMove(end, false);
        editor.PointerUp(end);

        Assert.Equal(panX + 50, editor.PanX, 2);
        Assert.Equal(panY + 50, editor.PanY, 2);
    }

    [Fact]
    public void ZoomToolZoomsInAndAltZoomsOut()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Zoom);
        float zoom = editor.Zoom;

        editor.PointerDown(new Vec2(300, 200), false, false, middle: false);
        Assert.True(editor.Zoom > zoom);

        float zoomed = editor.Zoom;
        editor.PointerDown(new Vec2(300, 200), false, alt: true, middle: false);
        Assert.True(editor.Zoom < zoomed);
    }

    [Fact]
    public void PointerMoveUpdatesTheCursorReadout()
    {
        FontEditor editor = Fx.Editor();

        editor.PointerMove(editor.View.ToScreen(120, 240), false);

        Assert.True(editor.CursorOnCanvas);
        Fx.Nearly(120f, editor.CursorUnits.X);
        Fx.Nearly(240f, editor.CursorUnits.Y);
    }

    [Fact]
    public void WheelZoomsAndHorizontalWheelPans()
    {
        FontEditor editor = Fx.Editor();
        float zoom = editor.Zoom;

        editor.Scroll(0, 120, new Vec2(300, 200), control: false, shift: false);
        Assert.True(editor.Zoom > zoom);

        float panX = editor.PanX;
        editor.Scroll(40, 0, new Vec2(300, 200), control: false, shift: false);
        Assert.Equal(panX - 40, editor.PanX);
    }

    [Fact]
    public void NudgeMovesSelectedNodesOrTheWholeGlyph()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.Nudge(new Vec2(10, -5));
        Assert.Equal(110f, glyph.OutlineBounds.MinX);
        Assert.Equal(-5f, glyph.OutlineBounds.MinY);

        editor.SelectNode(0, 0);
        editor.Nudge(new Vec2(5, 5));
        Assert.Equal(new Vec2(115, 0), glyph.Contours[0].Nodes[0].Point);
        Assert.Equal(new Vec2(510, 695), glyph.Contours[0].Nodes[2].Point);
    }

    [Fact]
    public void PointerReleaseWhileDrawingPenKeepsAShortHandleRetracted()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Vec2 point = new(100, 100);

        editor.PointerDown(editor.View.ToScreen(point), false, false, middle: false);
        editor.PointerUp(editor.View.ToScreen(point));

        GlyphNode node = editor.SelectedGlyph!.Contours[0].Nodes[0];
        Assert.False(node.HasIn);
        Assert.False(node.HasOut);
        Assert.False(node.Smooth);
    }
}

public sealed class FontEditorPenTests
{
    [Fact]
    public void PenPlacesNodesPullsHandlesAndClosesOnTheFirstPoint()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);

        Fx.Click(editor, new Vec2(100, 0));
        Assert.True(editor.IsPenActive);
        Assert.Equal(0, editor.SelectedContour);
        Assert.Single(editor.SelectedNodes);

        Fx.Click(editor, new Vec2(500, 0));
        Fx.Click(editor, new Vec2(300, 500));
        Assert.Equal(3, editor.SelectedGlyph!.Contours[0].Nodes.Count);

        Fx.Click(editor, new Vec2(100, 0));

        Assert.False(editor.IsPenActive);
        Assert.True(editor.SelectedGlyph!.Contours[0].Closed);
        Assert.Equal(3, editor.SelectedGlyph.Contours[0].Nodes.Count);
    }

    [Fact]
    public void DraggingWithThePenCreatesMirroredHandles()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);

        Fx.Drag(editor, new Vec2(100, 0), new Vec2(150, 80));

        GlyphNode node = editor.SelectedGlyph!.Contours[0].Nodes[0];
        Assert.True(node.HasOut);
        Assert.True(node.HasIn);
        Assert.True(node.Smooth);
        Fx.Nearly(150f, node.Out.X);
        Fx.Nearly(80f, node.Out.Y);
        Fx.Nearly(50f, node.In.X);
        Fx.Nearly(-80f, node.In.Y);
    }

    [Fact]
    public void ShiftSnapsNewPenPointsToTenUnits()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);

        Fx.Click(editor, new Vec2(123, 47), shift: true);

        Assert.Equal(new Vec2(120, 50), editor.SelectedGlyph!.Contours[0].Nodes[0].Point);    }

    [Fact]
    public void FinishPenDropsASinglePointContour()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 100));

        editor.FinishPen(close: false);

        Assert.Empty(editor.SelectedGlyph!.Contours);
        Assert.False(editor.IsPenActive);
    }

    [Fact]
    public void FinishPenLeavesATwoPointContourOpen()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 0));
        Fx.Click(editor, new Vec2(300, 200));

        editor.FinishPen(close: true);

        Assert.False(editor.SelectedGlyph!.Contours[0].Closed);
        Assert.Equal(2, editor.SelectedGlyph.Contours[0].Nodes.Count);
    }

    [Fact]
    public void ClosingAReverseWoundContourNormalisesItsDirection()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);

        Fx.Click(editor, new Vec2(0, 0));
        Fx.Click(editor, new Vec2(0, 300));
        Fx.Click(editor, new Vec2(300, 300));
        Fx.Click(editor, new Vec2(0, 0));

        GlyphContour contour = editor.SelectedGlyph!.Contours[0];
        float area = 0f;
        for (int i = 0; i < contour.Nodes.Count; i++)
        {
            Vec2 a = contour.Nodes[i].Point;
            Vec2 b = contour.Nodes[(i + 1) % contour.Nodes.Count].Point;
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        Assert.True(area > 0f);
    }

    [Fact]
    public void PenPreviewShowsTheLastPlacedPoint()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 100));

        Fx.Nearly(100f, editor.PenPreview!.Value.X);
        Fx.Nearly(100f, editor.PenPreview.Value.Y);

        editor.FinishPen(close: false);
        Assert.Null(editor.PenPreview);
    }

    [Fact]
    public void SwitchingToolsFinishesThePenStroke()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 0));
        Fx.Click(editor, new Vec2(300, 200));

        editor.SetTool(GlyphTool.Select);

        Assert.False(editor.IsPenActive);
        Assert.Single(editor.SelectedGlyph!.Contours);
    }
}

public sealed class FontEditorBrushTests
{
    [Fact]
    public void BrushDragBuildsAPreviewAndCommitsAContour()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);

        editor.PointerDown(editor.View.ToScreen(100, 100), false, false, middle: false);
        Assert.True(editor.IsBrushActive);
        Assert.Single(editor.BrushPoints);
        Assert.NotNull(editor.BrushPreview);

        editor.PointerMove(editor.View.ToScreen(300, 100), false);
        editor.PointerMove(editor.View.ToScreen(300, 300), false);
        Assert.True(editor.BrushPoints.Count >= 3);
        Assert.True(editor.BrushPreview!.HasOutline);

        editor.PointerUp(editor.View.ToScreen(300, 300));

        Assert.False(editor.IsBrushActive);
        Assert.Empty(editor.BrushPoints);
        Assert.Null(editor.BrushPreview);
        Assert.Single(editor.SelectedGlyph!.Contours);
        Assert.True(editor.SelectedGlyph.Contours[0].Closed);
        Assert.Contains("Brush stroke added", editor.Status);
    }

    [Fact]
    public void BrushRespectsTheMinimumSampleSpacing()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);
        editor.PointerDown(editor.View.ToScreen(100, 100), false, false, middle: false);
        int count = editor.BrushPoints.Count;

        editor.PointerMove(editor.View.ToScreen(100, 100), false);
        Assert.Equal(count, editor.BrushPoints.Count);

        editor.CancelBrush();
    }

    [Fact]
    public void CancelBrushDiscardsTheStroke()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);
        editor.PointerDown(editor.View.ToScreen(100, 100), false, false, middle: false);
        editor.PointerMove(editor.View.ToScreen(300, 100), false);

        editor.CancelBrush();

        Assert.False(editor.IsBrushActive);
        Assert.Empty(editor.BrushPoints);
        Assert.Null(editor.BrushPreview);
        Assert.False(editor.SelectedGlyph!.HasOutline);

        editor.CancelBrush();
    }

    [Fact]
    public void SwitchingAwayFromTheBrushCancelsIt()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);
        editor.PointerDown(editor.View.ToScreen(100, 100), false, false, middle: false);
        editor.PointerMove(editor.View.ToScreen(300, 100), false);

        editor.SetTool(GlyphTool.Pen);

        Assert.False(editor.IsBrushActive);
        Assert.False(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void BrushWidthAffectsTheCommittedContour()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Brush);
        editor.SetBrushWidth(40);
        Fx.Drag(editor, new Vec2(100, 100), new Vec2(400, 100));
        float narrow = editor.SelectedGlyph!.OutlineBounds.Height;

        editor.SelectGlyph('B');
        editor.SetBrushWidth(160);
        Fx.Drag(editor, new Vec2(100, 100), new Vec2(400, 100));
        float wide = editor.SelectedGlyph!.OutlineBounds.Height;

        Assert.True(wide > narrow);
    }
}

public sealed class FontEditorKeyboardTests
{
    [Theory]
    [InlineData("V", GlyphTool.Select)]
    [InlineData("A", GlyphTool.Node)]
    [InlineData("P", GlyphTool.Pen)]
    [InlineData("B", GlyphTool.Brush)]
    [InlineData("H", GlyphTool.Hand)]
    [InlineData("Z", GlyphTool.Zoom)]
    public void ToolKeysChooseTools(string chord, GlyphTool tool)
    {
        FontEditor editor = Fx.Editor();

        Assert.True(editor.HandleKey(chord));
        Assert.Equal(tool, editor.Tool);
    }

    [Fact]
    public void IsTypingSuspendsEveryShortcut()
    {
        FontEditor editor = Fx.Editor();
        editor.IsTyping = true;

        Assert.False(editor.HandleKey("B"));
        Assert.False(editor.HandleKey("Ctrl+Z"));
        Assert.Equal(GlyphTool.Node, editor.Tool);
    }

    [Fact]
    public void UnknownChordsAreNotConsumed()
    {
        FontEditor editor = Fx.Editor();

        Assert.False(editor.HandleKey("F1"));
        Assert.False(editor.HandleKey("Ctrl+F1"));
    }

    [Fact]
    public void UndoAndRedoShortcutsWork()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.PushUndo();
        editor.ClearGlyph();
        Assert.False(editor.SelectedGlyph!.HasOutline);

        Assert.True(editor.HandleKey("Ctrl+Z"));
        Assert.True(editor.SelectedGlyph!.HasOutline);

        Assert.True(editor.HandleKey("Ctrl+Shift+Z"));
        Assert.False(editor.SelectedGlyph!.HasOutline);

        Assert.True(editor.HandleKey("Ctrl+Z"));
        Assert.True(editor.HandleKey("Ctrl+Y"));
        Assert.False(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void CopyPasteAndDuplicateShortcutsWork()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.True(editor.HandleKey("Ctrl+C"));
        editor.SelectGlyph('B');
        Assert.True(editor.HandleKey("Ctrl+V"));
        Assert.True(editor.SelectedGlyph!.HasOutline);

        Assert.True(editor.HandleKey("Ctrl+D"));
        Assert.True(editor.ShowDuplicate);
    }

    [Fact]
    public void SelectAllAndDeleteShortcutsWork()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.True(editor.HandleKey("Ctrl+A"));
        Assert.Equal(4, editor.SelectedNodes.Count);

        Assert.True(editor.HandleKey("Delete"));
        Assert.False(editor.SelectedGlyph!.HasOutline);

        editor.Undo();
        editor.HandleKey("Ctrl+A");
        Assert.True(editor.HandleKey("Backspace"));
        Assert.False(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void ZoomShortcutsCoverFitInOutAndEquals()
    {
        FontEditor editor = Fx.Editor();
        editor.ZoomAt(0.5f / editor.Zoom, Vec2.Zero);

        Assert.True(editor.HandleKey("Ctrl+Equals"));
        Assert.Equal(0.65f, editor.Zoom, 3);
        Assert.True(editor.HandleKey("Ctrl+Plus"));
        Assert.True(editor.Zoom > 0.65f);
        Assert.True(editor.HandleKey("Ctrl+Minus"));
        Assert.True(editor.HandleKey("Ctrl+0"));
        Assert.True(editor.Zoom > 0f);
    }

    [Fact]
    public void FileAndExportShortcutsOpenTheRightSurfaces()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();

        Assert.True(editor.HandleKey("Ctrl+N"));
        Assert.True(editor.ShowNewFont);
        editor.CloseDialog();

        Assert.True(editor.HandleKey("Ctrl+O"));
        Assert.True(editor.ShowFileBrowser);
        editor.CloseFileBrowser();

        Assert.True(editor.HandleKey("Ctrl+E"));
        Assert.True(editor.ShowExport);
        editor.CloseDialog();

        editor.Project.Path = temp.File("quick.hgl");
        Assert.True(editor.HandleKey("Ctrl+S"));
        Assert.True(File.Exists(editor.Project.Path));
    }

    [Fact]
    public void EscapeClosesTheMenuThenTheBrowserThenThePenThenTheSelection()
    {
        FontEditor editor = Fx.Editor();
        editor.ToggleMenuAt("File", 10, 10);
        Assert.True(editor.HandleKey("Escape"));
        Assert.Null(editor.OpenMenu);

        editor.OpenProject();
        Assert.True(editor.HandleKey("Escape"));
        Assert.False(editor.ShowFileBrowser);

        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 0));
        Assert.True(editor.HandleKey("Escape"));
        Assert.False(editor.IsPenActive);
        Assert.Empty(editor.SelectedGlyph!.Contours);

        editor.SetTool(GlyphTool.Brush);
        editor.PointerDown(editor.View.ToScreen(100, 100), false, false, middle: false);
        editor.PointerMove(editor.View.ToScreen(300, 100), false);
        Assert.True(editor.HandleKey("Escape"));
        Assert.False(editor.IsBrushActive);

        editor.ClearSelection();
        editor.SelectNode(0, 0);
        Assert.True(editor.HandleKey("Escape"));
        Assert.Empty(editor.SelectedNodes);

        Assert.True(editor.HandleKey("Escape"));
    }

    [Fact]
    public void EnterFinishesThePenOrConfirmsTheFileBrowser()
    {
        FontEditor editor = Fx.Editor();
        editor.SetTool(GlyphTool.Pen);
        Fx.Click(editor, new Vec2(100, 0));
        Fx.Click(editor, new Vec2(300, 200));

        Assert.True(editor.HandleKey("Enter"));
        Assert.False(editor.IsPenActive);
        Assert.Single(editor.SelectedGlyph!.Contours);

        editor.OpenProject();
        Assert.True(editor.HandleKey("Enter"));
        Assert.Contains("Choose a project file", editor.BrowserStatus);

        editor.CloseFileBrowser();
        Assert.False(editor.HandleKey("Enter"));
    }

    [Fact]
    public void BracketAndPageKeysStepThroughCharacters()
    {
        FontEditor editor = Fx.Editor();
        editor.SelectGlyph('B');

        Assert.True(editor.HandleKey("]"));
        Assert.Equal('C', editor.SelectedCode);
        Assert.True(editor.HandleKey("["));
        Assert.Equal('B', editor.SelectedCode);
        Assert.True(editor.HandleKey("BracketRight"));
        Assert.Equal('C', editor.SelectedCode);
        Assert.True(editor.HandleKey("BracketLeft"));
        Assert.Equal('B', editor.SelectedCode);

        Assert.True(editor.HandleKey("PageDown"));
        Assert.Equal('C', editor.SelectedCode);
        Assert.True(editor.HandleKey("PageUp"));
        Assert.Equal('B', editor.SelectedCode);
    }

    [Theory]
    [InlineData("ArrowLeft", -10f, 0f)]
    [InlineData("Left", -10f, 0f)]
    [InlineData("ArrowRight", 10f, 0f)]
    [InlineData("Right", 10f, 0f)]
    [InlineData("ArrowUp", 0f, 10f)]
    [InlineData("Up", 0f, 10f)]
    [InlineData("ArrowDown", 0f, -10f)]
    [InlineData("Down", 0f, -10f)]
    public void ArrowKeysNudgeByTenUnits(string chord, float dx, float dy)
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        Assert.True(editor.HandleKey(chord));

        Assert.Equal(100f + dx, glyph.OutlineBounds.MinX);
        Assert.Equal(0f + dy, glyph.OutlineBounds.MinY);
    }

    [Fact]
    public void ShiftArrowsNudgeByOneUnit()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.HandleKey("Shift+ArrowRight");
        editor.HandleKey("Shift+ArrowDown");

        Assert.Equal(501f, glyph.OutlineBounds.MaxX);
        Assert.Equal(-1f, glyph.OutlineBounds.MinY);
    }

    [Fact]
    public void NumberZeroIsNormalisedForZoomShortcuts()
    {
        FontEditor editor = Fx.Editor();
        editor.ZoomAt(0.5f / editor.Zoom, Vec2.Zero);

        Assert.True(editor.HandleKey("Ctrl+Number0"));
        Assert.NotEqual(0.5f, editor.Zoom);
    }
}
