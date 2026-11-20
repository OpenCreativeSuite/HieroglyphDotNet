using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void VecArithmeticAndHelpersWork()
    {
        Vec2 a = new(3, 4);
        Vec2 b = new(1, 2);

        Assert.Equal(5f, a.Length, 4);
        Assert.Equal(new Vec2(4, 6), a + b);
        Assert.Equal(new Vec2(2, 2), a - b);
        Assert.Equal(new Vec2(-3, -4), -a);
        Assert.Equal(new Vec2(6, 8), a * 2f);
        Assert.Equal(new Vec2(1.5f, 2f), a / 2f);
        Assert.Equal(new Vec2(-4, 3), a.Perp);
        Assert.Equal(new Vec2(1.5f, 2f), a.Offset(-1.5f, -2f));
        Assert.Equal(8f, a.DistanceSquared(b));
        Assert.Equal(2.828427f, a.DistanceTo(b), 3);
        Assert.Equal(new Vec2(2, 3), Vec2.Lerp(a, b, 0.5f));
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized());
        Assert.Equal(new Vec2(5, 5), new Vec2(4.6f, 5.4f).Rounded());
    }

    [Fact]
    public void RotatedMovesAroundAPivot()
    {
        Vec2 rotated = new Vec2(10, 5).Rotated(new Vec2(5, 5), MathF.PI / 2f);
        Fx.Nearly(5f, rotated.X);
        Fx.Nearly(10f, rotated.Y);
    }

    [Fact]
    public void BoundsAbsorbPointsAndSupportQueries()
    {
        Bounds bounds = Bounds.Empty;
        bounds = bounds.With(new Vec2(10, 20));
        bounds = bounds.With(new Vec2(4, 30));
        bounds = bounds.With(new Vec2(20, 10));

        Assert.Equal(4f, bounds.MinX);
        Assert.Equal(10f, bounds.MinY);
        Assert.Equal(20f, bounds.MaxX);
        Assert.Equal(30f, bounds.MaxY);
        Assert.Equal(16f, bounds.Width, 3);
        Assert.Equal(20f, bounds.Height, 3);
        Assert.Equal(new Vec2(12, 20), bounds.Center);
        Assert.True(bounds.Contains(new Vec2(10, 20)));
        Assert.False(bounds.Contains(new Vec2(0, 0)));
        Assert.True(bounds.Contains(new Vec2(3, 9), 1f));
        Assert.True(bounds.Intersects(new Bounds(19, 29, 34, 34)));
        Assert.False(bounds.Intersects(new Bounds(30, 40, 35, 45)));
        Assert.Equal(new Bounds(1, 7, 23, 33), bounds.Inflate(3));
        Assert.Equal(new Bounds(14, 20, 30, 40), bounds.Translate(10, 10));
        Assert.True(Bounds.Empty.IsEmpty);
        Assert.True(Bounds.Zero.IsEmpty);
    }

    [Fact]
    public void BoundsUnionIgnoresEmptyOperands()
    {
        Bounds a = new(0, 0, 10, 10);
        Bounds union = a.Union(new Bounds(5, 5, 5, 5));

        Assert.Equal(new Bounds(0, 0, 10, 10), union);
        Assert.Equal(a, a.Union(Bounds.Empty));
    }

    [Fact]
    public void GlyphViewMapsBetweenUnitsAndPixels()
    {
        GlyphView view = new(0.5f, 100f, 700f);

        Assert.Equal(new Vec2(150, 650), view.ToScreen(new Vec2(100, 100)));
        Assert.Equal(new Vec2(150, 650), view.ToScreen(100f, 100f));
        Assert.Equal(new Vec2(100, 100), view.ToUnits(new Vec2(150, 650)));
        Assert.Equal(new GlyphView(0.5f, 20f, 30f).PanX, view.WithPan(20, 30).PanX);
    }
}

public sealed class GlyphModelTests
{
    [Fact]
    public void NodeFactoriesCloneTranslateAndMirror()
    {
        GlyphNode corner = GlyphNode.Corner(new Vec2(10, 20));
        Assert.False(corner.IsCurved);

        GlyphNode smooth = GlyphNode.SmoothAt(new Vec2(10, 20), new Vec2(1, 0), 4f, 6f);
        Assert.True(smooth.Smooth);
        Assert.True(smooth.IsCurved);
        Assert.Equal(new Vec2(6, 20), smooth.In);
        Assert.Equal(new Vec2(16, 20), smooth.Out);

        smooth.Translate(new Vec2(5, 5));
        Assert.Equal(new Vec2(15, 25), smooth.Point);

        smooth.Out = new Vec2(25, 25);
        smooth.MirrorOut();
        Assert.Equal(new Vec2(5, 25), smooth.In);

        smooth.In = new Vec2(1, 1);
        smooth.MirrorIn();
        Assert.Equal(new Vec2(29, 49), smooth.Out);

        GlyphNode clone = smooth.Clone();
        clone.Point = Vec2.Zero;
        Assert.Equal(new Vec2(15, 25), smooth.Point);

        GlyphNode corner2 = GlyphNode.Corner(new Vec2(1, 1));
        corner2.Out = new Vec2(5, 5);
        corner2.HasOut = true;
        corner2.MirrorOut();
        Assert.False(corner2.HasIn);
    }

    [Fact]
    public void ContourBoundsIncludeHandlesAndReverseSwapsThem()
    {
        GlyphContour contour = new();
        contour.Nodes.Add(new GlyphNode
        {
            Point = new Vec2(10, 10),
            In = new Vec2(5, 5),
            Out = new Vec2(15, 15),
            HasIn = true,
            HasOut = true,
        });
        contour.Nodes.Add(GlyphNode.Corner(new Vec2(20, 20)));

        Assert.True(contour.IsDrawable);
        Assert.False(contour.IsEmpty);
        Assert.Equal(new Bounds(5, 5, 20, 20), contour.Bounds);

        GlyphContour clone = contour.Clone();
        Assert.NotSame(contour.Nodes[0], clone.Nodes[0]);

        contour.Reverse();
        Assert.Equal(new Vec2(20, 20), contour.Nodes[0].Point);
        Assert.Equal(new Vec2(10, 10), contour.Nodes[1].Point);
        Assert.Equal(new Vec2(15, 15), contour.Nodes[1].In);
        Assert.Equal(new Vec2(5, 5), contour.Nodes[1].Out);
    }

    [Fact]
    public void GlyphReportsNamesBoundsAndBearings()
    {
        Glyph glyph = Glyph.Blank('A', "Capital A", 640);
        Assert.Equal("A", glyph.Character);
        Assert.True(glyph.IsBlank);
        Assert.Equal(0, glyph.NodeCount);
        Assert.Equal(640, glyph.RightSideBearing);

        glyph.Contours.Add(new GlyphContour
        {
            Nodes =
            [
                GlyphNode.Corner(new Vec2(50, 0)),
                GlyphNode.Corner(new Vec2(550, 700)),
                GlyphNode.Corner(new Vec2(50, 700)),
            ],
        });

        Assert.True(glyph.HasOutline);
        Assert.Equal(50f, glyph.LeftEdge);
        Assert.Equal(550f, glyph.RightEdge);
        Assert.Equal(50, glyph.LeftSideBearing);
        Assert.Equal(90, glyph.RightSideBearing);
        Assert.Equal(3, glyph.NodeCount);
        Assert.Equal(new Bounds(50, 0, 550, 700), glyph.OutlineBounds);
    }

    [Fact]
    public void GlyphCloneAndCloneForAreIndependent()
    {
        Glyph glyph = Fx.DrawRect(Fx.Editor(), 'M', 10, 0, 500, 700);
        Vec2 original = glyph.Contours[0].Nodes[0].Point;
        Glyph clone = glyph.Clone();
        clone.Contours[0].Nodes[0].Point = Vec2.Zero;
        Assert.Equal(original, glyph.Contours[0].Nodes[0].Point);

        Glyph copy = glyph.CloneFor('N', "Capital N", 700);
        Assert.Equal('N', copy.Codepoint);
        Assert.Equal("Capital N", copy.Name);
        Assert.Equal(700, copy.AdvanceWidth);
        Assert.NotSame(glyph.Contours[0], copy.Contours[0]);
    }

    [Fact]
    public void GlyphTransformsMoveEveryPointAndHandle()
    {
        Glyph glyph = Fx.DrawRect(Fx.Editor(), 'H', 100, 200, 400, 600);
        Bounds before = glyph.OutlineBounds;

        glyph.Translate(new Vec2(10, -5));
        Fx.Nearly(before.MinX + 10, glyph.OutlineBounds.MinX);
        Fx.Nearly(before.MaxY - 5, glyph.OutlineBounds.MaxY);

        glyph.Scale(Vec2.Zero, 2f, 0.5f);
        Fx.Nearly((before.MinX + 10) * 2, glyph.OutlineBounds.MinX);
        Fx.Nearly((before.MaxY - 5) * 0.5f, glyph.OutlineBounds.MaxY);
    }

    [Fact]
    public void FlipAndRotatePreserveGeometryShape()
    {
        Glyph horizontal = Fx.DrawRect(Fx.Editor(), 'J', 100, 100, 500, 600, advance: 600);
        float width = horizontal.OutlineBounds.Width;
        horizontal.FlipHorizontal(300);
        Fx.Nearly(width, horizontal.OutlineBounds.Width);
        Fx.Nearly(100, horizontal.OutlineBounds.MinX);
        Fx.Nearly(500, horizontal.OutlineBounds.MaxX);

        Glyph vertical = Fx.DrawRect(Fx.Editor(), 'K', 100, 100, 500, 600);
        float height = vertical.OutlineBounds.Height;
        vertical.FlipVertical(300);
        Fx.Nearly(height, vertical.OutlineBounds.Height);

        Glyph rotated = Fx.DrawRect(Fx.Editor(), 'L', 100, 100, 300, 500);
        rotated.RotateQuarter(new Vec2(200, 300), clockwise: true);
        Fx.Nearly(400f, rotated.OutlineBounds.Width, 5f);
    }

    [Fact]
    public void SurrogateCodepointsReportTheirUtf16Need()
    {
        Glyph astral = Glyph.Blank(0x1F600, "Emoji", 800);

        Assert.True(astral.IsSurrogate);
        Assert.Equal(char.ConvertFromUtf32(0x1F600), astral.Character);
    }
}
