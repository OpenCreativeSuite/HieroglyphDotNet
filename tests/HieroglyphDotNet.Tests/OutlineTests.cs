using System.Numerics;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class OutlineBuilderTests
{
    [Fact]
    public void RectCreatesOneClosedFourNodeContour()
    {
        OutlineBuilder builder = new();
        builder.Rect(10, 20, 110, 220);

        Assert.Single(builder.Contours);
        GlyphContour contour = builder.Contours[0];
        Assert.True(contour.Closed);
        Assert.Equal(4, contour.Nodes.Count);
        Assert.Equal(new Bounds(10, 20, 110, 220), contour.Bounds);
    }

    [Fact]
    public void PolygonIgnoresUndersizedInputAndNormalisesWinding()
    {
        OutlineBuilder builder = new();
        builder.Polygon([new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10)], hole: false);
        builder.Polygon([new Vec2(2, 2), new Vec2(8, 2), new Vec2(8, 8), new Vec2(2, 8)], hole: true);
        builder.Polygon([new Vec2(0, 0), new Vec2(1, 1)]);

        Assert.Equal(2, builder.Contours.Count);
        Assert.True(Area(builder.Contours[0]) > 0f);
        Assert.True(Area(builder.Contours[1]) < 0f);
    }

    [Fact]
    public void PrimitivesProduceTheExpectedContours()
    {
        OutlineBuilder builder = new();
        builder.VStem(300, 0, 700, 60);
        builder.HBar(100, 350, 500, 40);
        builder.Disc(new Vec2(300, 300), 100);
        builder.Ellipse(new Vec2(300, 300), 120, 80);
        builder.Ring(new Vec2(300, 300), 200, 200, 40);
        builder.Band(new Vec2(100, 100), new Vec2(500, 100), 50, roundStart: true, roundEnd: true);
        builder.Chain([new Vec2(100, 100), new Vec2(200, 200), new Vec2(300, 100)], 40);
        builder.ArcBand(new Vec2(300, 300), 200, 150, 50, 0, 180);

        Assert.True(builder.Contours.Count >= 10);
        Assert.All(builder.Contours, contour => Assert.True(contour.Closed));
    }

    [Fact]
    public void BandIgnoresZeroLengthAndChainSkipsJoints()
    {
        OutlineBuilder builder = new();
        builder.Band(new Vec2(10, 10), new Vec2(10, 10), 20);
        Assert.Empty(builder.Contours);

        builder.Chain([new Vec2(10, 10), new Vec2(20, 20), new Vec2(30, 10)], 20);
        Assert.True(builder.Contours.Count >= 3);
    }

    [Fact]
    public void RingSkipsAnImpossiblyThinHole()
    {
        OutlineBuilder builder = new();
        builder.Ring(new Vec2(0, 0), 10, 10, 18);

        Assert.Single(builder.Contours);
    }

    [Fact]
    public void ArcSegmentsApproximateACircle()
    {
        List<CubicSegment> full = OutlineBuilder.ArcSegments(new Vec2(0, 0), 100, 100, 0, MathF.Tau);
        Assert.Equal(4, full.Count);

        Fx.Nearly(100f, full[0].Start.X);
        Fx.Nearly(0f, full[0].Start.Y);
        Fx.Nearly(100f, full[^1].End.X);
        Fx.Nearly(0f, full[^1].End.Y, 0.001f);
        Assert.Equal(new Vec2(100, 0), OutlineBuilder.PointAt(new Vec2(0, 0), 100, 50, 0));
        Assert.Empty(OutlineBuilder.ArcSegments(new Vec2(0, 0), 0, 50, 0, MathF.Tau));
    }

    [Fact]
    public void OutlineWithCurvesSetsNodeHandles()
    {
        OutlineBuilder builder = new();
        List<CubicSegment> segments =
        [
            new(new Vec2(0, 0), new Vec2(20, 0), new Vec2(80, 0), new Vec2(100, 0)),
            new(new Vec2(100, 0), new Vec2(100, 0), new Vec2(50, 100), new Vec2(0, 0)),
        ];
        builder.Outline(segments);

        Assert.Single(builder.Contours);
        Assert.True(builder.Contours[0].Nodes[0].HasOut);
        Assert.True(builder.Contours[0].Nodes[1].HasIn);
    }

    private static float Area(GlyphContour contour)
    {
        float sum = 0f;
        for (int i = 0; i < contour.Nodes.Count; i++)
        {
            Vec2 a = contour.Nodes[i].Point;
            Vec2 b = contour.Nodes[(i + 1) % contour.Nodes.Count].Point;
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum * 0.5f;
    }
}

public sealed class StrokeOutlineTests
{
    [Fact]
    public void EmptyStrokeBuildsNothing()
    {
        Assert.Empty(StrokeOutline.Build([], 40));
    }

    [Fact]
    public void SinglePointBuildsADisc()
    {
        List<GlyphContour> contours = StrokeOutline.Build([new Vec2(10, 20)], 40);

        Assert.Single(contours);
        Assert.True(contours[0].Closed);
        Assert.True(contours[0].Bounds.Width > 30f);
        Assert.True(contours[0].Bounds.Height > 30f);
    }

    [Fact]
    public void TwoPointStrokeBuildsASmoothClosedCapsule()
    {
        List<GlyphContour> contours = StrokeOutline.Build(
            [new Vec2(0, 0), new Vec2(200, 0)],
            40);

        Assert.Single(contours);
        GlyphContour contour = contours[0];
        Assert.True(contour.Closed);
        Assert.True(contour.Nodes.Count > 4);
        Assert.All(contour.Nodes, node => Assert.True(node.Smooth && node.HasIn && node.HasOut));
        Assert.True(contour.Bounds.Width >= 200f);
        Assert.True(contour.Bounds.Height >= 20f);
    }

    [Fact]
    public void LongerFreehandStrokeIsSmoothedAndOriented()
    {
        List<GlyphContour> contours = StrokeOutline.Build(
            [
                new Vec2(0, 0),
                new Vec2(40, 60),
                new Vec2(80, 20),
                new Vec2(120, 80),
                new Vec2(160, 0),
            ],
            60);

        Assert.Single(contours);
        GlyphContour contour = contours[0];
        Assert.True(contour.Bounds.Width > 120f);
        Assert.True(contour.Nodes.Count >= 3);
    }
}

public sealed class GlyphRasterizerTests
{
    [Fact]
    public void FlattenWalksLinesAndCurves()
    {
        GlyphContour line = new();
        line.Nodes.Add(GlyphNode.Corner(new Vec2(0, 0)));
        line.Nodes.Add(GlyphNode.Corner(new Vec2(100, 0)));
        line.Nodes.Add(GlyphNode.Corner(new Vec2(100, 100)));
        line.Closed = true;

        List<Vec2> flat = GlyphRasterizer.Flatten(line, 1f, 0f, 100f);
        Assert.Equal(4, flat.Count);
        Assert.Equal(new Vec2(0, 100), flat[0]);
        Assert.Equal(new Vec2(100, 100), flat[1]);

        GlyphContour curve = new();
        curve.Nodes.Add(new GlyphNode
        {
            Point = new Vec2(0, 0),
            Out = new Vec2(50, 100),
            HasOut = true,
        });
        curve.Nodes.Add(new GlyphNode
        {
            Point = new Vec2(100, 0),
            In = new Vec2(50, 100),
            HasIn = true,
        });
        curve.Closed = false;

        List<Vec2> curved = GlyphRasterizer.Flatten(curve, 1f, 0f, 100f, forceClose: true);
        Assert.True(curved.Count > 3);
        Assert.Equal(curved[0], curved[^1]);
    }

    [Fact]
    public void CubicPointInterpolatesEndpoints()
    {
        Vec2 p0 = new(0, 0);
        Vec2 p1 = new(25, 50);
        Vec2 p2 = new(75, 50);
        Vec2 p3 = new(100, 0);

        Assert.Equal(p0, GlyphRasterizer.Cubic(p0, p1, p2, p3, 0f));
        Assert.Equal(p3, GlyphRasterizer.Cubic(p0, p1, p2, p3, 1f));
        Fx.Nearly(50f, GlyphRasterizer.Cubic(p0, p1, p2, p3, 0.5f).X);
        Fx.Nearly(37.5f, GlyphRasterizer.Cubic(p0, p1, p2, p3, 0.5f).Y);
    }

    [Fact]
    public void RasterizeFillsRectanglesWithCoverageRuns()
    {
        Glyph glyph = Fx.DrawRect(Fx.Editor(), 'A', 100, 0, 500, 700);

        GlyphBitmap bitmap = GlyphRasterizer.Rasterize(glyph, 0.1f);

        Assert.True(bitmap.Width > 20);
        Assert.True(bitmap.Height > 20);
        Assert.NotEmpty(bitmap.Runs);
        Assert.All(bitmap.Runs, run => Assert.InRange(run.Coverage, 1, 255));
        Assert.Contains(bitmap.Runs, run => run.Coverage > 200);
        Assert.All(bitmap.Runs, run => Assert.InRange(run.X, 0, bitmap.Width - 1));
        Assert.All(bitmap.Runs, run => Assert.InRange(run.Y, 0, bitmap.Height - 1));
        Assert.True(bitmap.OriginY > 0);
        Assert.True(bitmap.OriginX < bitmap.Width);
    }

    [Fact]
    public void BlankGlyphGetsAnEmptyBitmap()
    {
        GlyphBitmap bitmap = GlyphRasterizer.Rasterize(Glyph.Blank('X', "X", 600), 0.1f);

        Assert.Equal(1, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        Assert.Empty(bitmap.Runs);
    }

    [Fact]
    public void RasterizeRespectsHoles()
    {
        Glyph glyph = Fx.DrawRing(Fx.Editor(), 'O', 200, 60);
        GlyphBitmap bitmap = GlyphRasterizer.Rasterize(glyph, 0.2f);

        int centreX = bitmap.OriginX + 60;
        int centreY = bitmap.OriginY - 60;
        bool centreCovered = bitmap.Runs.Any(run =>
            run.Y == centreY && centreX >= run.X && centreX < run.X + run.Width);
        Assert.False(centreCovered);
    }

    [Fact]
    public void CacheReusesBitmapsUntilSyncedOrCleared()
    {
        Glyph glyph = Fx.DrawRect(Fx.Editor(), 'A', 100, 0, 500, 700);
        GlyphRasterCache cache = new();

        GlyphBitmap first = cache.Get(glyph, 0.1f);
        GlyphBitmap second = cache.Get(glyph, 0.1f);
        Assert.Same(first, second);

        cache.Sync(1);
        GlyphBitmap afterSync = cache.Get(glyph, 0.1f);
        Assert.NotSame(first, afterSync);

        cache.Sync(1);
        GlyphBitmap sameVersion = cache.Get(glyph, 0.1f);
        Assert.Same(afterSync, sameVersion);

        cache.Sync(2);
        GlyphBitmap afterVersion = cache.Get(glyph, 0.1f);
        Assert.NotSame(afterSync, afterVersion);

        cache.Clear();
        GlyphBitmap afterClear = cache.Get(glyph, 0.1f);
        Assert.NotSame(afterVersion, afterClear);
    }
}
