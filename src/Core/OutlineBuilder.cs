namespace HieroglyphDotNet.Core;

/// <summary>One cubic Bézier segment of an outline.</summary>
public readonly record struct CubicSegment(Vec2 Start, Vec2 Control1, Vec2 Control2, Vec2 End);

/// <summary>Builds glyph outlines from geometric primitives.</summary>
/// <remarks>
/// Every primitive is emitted as its own contour with a consistent winding
/// direction. Overlapping contours therefore merge under the non-zero fill rule
/// without boolean path operations, which is how the starter typeface is
/// assembled from stems, bands and rings.
/// </remarks>
public sealed class OutlineBuilder
{
    private readonly List<GlyphContour> _contours = [];

    /// <summary>The contours built so far.</summary>
    public IReadOnlyList<GlyphContour> Contours => _contours;

    /// <summary>Appends a closed contour built from cubic segments.</summary>
    public void Outline(IReadOnlyList<CubicSegment> segments, bool hole = false)
    {
        if (segments.Count < 2)
        {
            return;
        }

        GlyphContour contour = new() { Closed = true };
        foreach (CubicSegment segment in segments)
        {
            contour.Nodes.Add(GlyphNode.Corner(segment.Start));
        }

        for (int i = 0; i < segments.Count; i++)
        {
            GlyphNode node = contour.Nodes[i];
            CubicSegment segment = segments[i];
            if (segment.Control1 != segment.Start)
            {
                node.Out = segment.Control1;
                node.HasOut = true;
            }

            GlyphNode next = contour.Nodes[(i + 1) % contour.Nodes.Count];
            if (segment.Control2 != segment.End)
            {
                next.In = segment.Control2;
                next.HasIn = true;
            }
        }

        Normalise(contour, hole);
        _contours.Add(contour);
    }

    /// <summary>Appends a straight-edged polygon.</summary>
    public void Polygon(IReadOnlyList<Vec2> points, bool hole = false)
    {
        if (points.Count < 3)
        {
            return;
        }

        GlyphContour contour = new() { Closed = true };
        foreach (Vec2 point in points)
        {
            contour.Nodes.Add(GlyphNode.Corner(point));
        }

        Normalise(contour, hole);
        _contours.Add(contour);
    }

    /// <summary>Adds a rectangle.</summary>
    public void Rect(float x0, float y0, float x1, float y1)
    {
        float left = MathF.Min(x0, x1);
        float right = MathF.Max(x0, x1);
        float bottom = MathF.Min(y0, y1);
        float top = MathF.Max(y0, y1);

        Polygon(
        [
            new Vec2(left, bottom),
            new Vec2(right, bottom),
            new Vec2(right, top),
            new Vec2(left, top),
        ]);
    }

    /// <summary>Adds a vertical stem with flat ends.</summary>
    public void VStem(float centreX, float bottom, float top, float width) =>
        Rect(centreX - (width * 0.5f), bottom, centreX + (width * 0.5f), top);

    /// <summary>Adds a horizontal bar with flat ends.</summary>
    public void HBar(float left, float centreY, float right, float width) =>
        Rect(left, centreY - (width * 0.5f), right, centreY + (width * 0.5f));

    /// <summary>Adds a straight stroke of constant width, optionally with round ends.</summary>
    public void Band(Vec2 from, Vec2 to, float width, bool roundStart = false, bool roundEnd = false)
    {
        Vec2 direction = (to - from).Normalized();
        if (direction == Vec2.Zero)
        {
            return;
        }

        Vec2 normal = direction.Perp * (width * 0.5f);
        Polygon(
        [
            from + normal,
            to + normal,
            to - normal,
            from - normal,
        ]);

        if (roundStart)
        {
            Disc(from, width * 0.5f);
        }

        if (roundEnd)
        {
            Disc(to, width * 0.5f);
        }
    }

    /// <summary>Adds a connected chain of straight strokes with round joints.</summary>
    public void Chain(IReadOnlyList<Vec2> points, float width, bool roundStart = true, bool roundEnd = true)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Band(points[i], points[i + 1], width, i == 0 && roundStart, i + 2 == points.Count && roundEnd);
        }

        for (int i = 1; i + 1 < points.Count; i++)
        {
            Disc(points[i], width * 0.5f);
        }
    }

    /// <summary>Adds a filled disc.</summary>
    public void Disc(Vec2 centre, float radius) => Ellipse(centre, radius, radius);

    /// <summary>Adds a filled ellipse.</summary>
    public void Ellipse(Vec2 centre, float radiusX, float radiusY) =>
        Outline(ArcSegments(centre, radiusX, radiusY, 0f, MathF.Tau));

    /// <summary>Adds an elliptical annulus: an outer ring with a matching hole.</summary>
    public void Ring(Vec2 centre, float radiusX, float radiusY, float thickness)
    {
        float half = thickness * 0.5f;
        float innerX = radiusX - half;
        float innerY = radiusY - half;

        Outline(ArcSegments(centre, radiusX + half, radiusY + half, 0f, MathF.Tau));

        if (innerX > 2f && innerY > 2f)
        {
            Outline(ArcSegments(centre, innerX, innerY, 0f, MathF.Tau), hole: true);
        }
    }

    /// <summary>Adds an elliptical arc band with round or flat terminals.</summary>
    public void ArcBand(
        Vec2 centre,
        float radiusX,
        float radiusY,
        float thickness,
        float startDegrees,
        float endDegrees,
        bool roundStart = true,
        bool roundEnd = true)
    {
        float half = thickness * 0.5f;
        float a0 = startDegrees * MathF.PI / 180f;
        float a1 = endDegrees * MathF.PI / 180f;
        float sweep = a1 - a0;
        if (MathF.Abs(sweep) < 1e-4f)
        {
            return;
        }

        float sign = MathF.Sign(sweep);

        Vec2 At(float angle, float offsetX, float offsetY) => new(
            centre.X + ((radiusX + offsetX) * MathF.Cos(angle)),
            centre.Y + ((radiusY + offsetY) * MathF.Sin(angle)));

        List<CubicSegment> segments = [];
        segments.AddRange(ArcSegments(centre, radiusX + half, radiusY + half, a0, a1));

        if (roundEnd)
        {
            segments.AddRange(ArcSegments(At(a1, 0f, 0f), half, half, a1, a1 + (MathF.PI * sign)));
        }
        else
        {
            segments.Add(new CubicSegment(At(a1, half, half), Vec2.Zero, Vec2.Zero, At(a1, -half, -half)));
        }

        segments.AddRange(ArcSegments(centre, radiusX - half, radiusY - half, a1, a0));

        if (roundStart)
        {
            segments.AddRange(ArcSegments(At(a0, 0f, 0f), half, half, a0 + (MathF.PI * sign), a0));
        }
        else
        {
            segments.Add(new CubicSegment(At(a0, -half, -half), Vec2.Zero, Vec2.Zero, At(a0, half, half)));
        }

        Outline(segments);
    }

    /// <summary>Cubic segments approximating an elliptical arc.</summary>
    public static List<CubicSegment> ArcSegments(Vec2 centre, float radiusX, float radiusY, float a0, float a1)
    {
        List<CubicSegment> segments = [];
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return segments;
        }

        float sweep = a1 - a0;
        int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweep) / (MathF.PI * 0.5f)));
        float step = sweep / steps;
        float kappa = (4f / 3f) * MathF.Tan(step * 0.25f);

        for (int i = 0; i < steps; i++)
        {
            float start = a0 + (step * i);
            float end = start + step;
            Vec2 p0 = PointAt(centre, radiusX, radiusY, start);
            Vec2 p3 = PointAt(centre, radiusX, radiusY, end);
            Vec2 d0 = new(-radiusX * MathF.Sin(start), radiusY * MathF.Cos(start));
            Vec2 d1 = new(-radiusX * MathF.Sin(end), radiusY * MathF.Cos(end));
            segments.Add(new CubicSegment(p0, p0 + (d0 * kappa), p3 - (d1 * kappa), p3));
        }

        return segments;
    }

    /// <summary>The point on an ellipse at an angle.</summary>
    public static Vec2 PointAt(Vec2 centre, float radiusX, float radiusY, float angle) =>
        new(centre.X + (radiusX * MathF.Cos(angle)), centre.Y + (radiusY * MathF.Sin(angle)));

    private static void Normalise(GlyphContour contour, bool hole)
    {
        bool clockwise = SignedArea(contour) < 0f;
        if (clockwise != hole)
        {
            contour.Reverse();
        }
    }

    private static float SignedArea(GlyphContour contour)
    {
        float sum = 0f;
        List<GlyphNode> nodes = contour.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            Vec2 a = nodes[i].Point;
            Vec2 b = nodes[(i + 1) % nodes.Count].Point;
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum * 0.5f;
    }
}
