namespace HieroglyphDotNet.Core;

/// <summary>Turns a freehand pointer path into a filled, editable brush outline.</summary>
/// <remarks>
/// The path is simplified and smoothed first, offset to both sides by half the
/// brush width, capped with semicircles, then rebuilt as a small number of
/// smooth cubic nodes. The result is one closed contour that behaves like any
/// other glyph outline: it can be filled, edited point by point and exported.
/// </remarks>
public static class StrokeOutline
{
    /// <summary>Builds the outline for a freehand stroke.</summary>
    public static List<GlyphContour> Build(IReadOnlyList<Vec2> raw, float width)
    {
        float half = MathF.Max(0.5f, width * 0.5f);
        List<Vec2> points = Simplify([.. raw], MathF.Max(0.7f, half * 0.08f));

        if (points.Count == 0)
        {
            return [];
        }

        if (points.Count == 1)
        {
            return [Disc(points[0], half)];
        }

        // Two rounds of corner cutting remove the polygonal feel of pointer
        // sampling without rounding away intentional shapes.
        points = Chaikin(points, 2);

        List<Vec2> polygon = [];
        int count = points.Count;

        for (int i = 0; i < count; i++)
        {
            polygon.Add(points[i] + (Normal(points, i) * half));
        }

        AddCap(polygon, points[^1], (points[^1] - points[^2]).Normalized(), half, forward: true);

        for (int i = count - 1; i >= 0; i--)
        {
            polygon.Add(points[i] - (Normal(points, i) * half));
        }

        AddCap(polygon, points[0], (points[1] - points[0]).Normalized(), half, forward: false);

        List<Vec2> simplified = Simplify(polygon, MathF.Max(0.9f, half * 0.05f));
        if (simplified.Count < 3)
        {
            return [Disc(points[0], half)];
        }

        return [Smooth(simplified)];
    }

    /// <summary>The perpendicular of the local direction at a point.</summary>
    private static Vec2 Normal(IReadOnlyList<Vec2> points, int index)
    {
        Vec2 direction;
        if (index == 0)
        {
            direction = points[1] - points[0];
        }
        else if (index == points.Count - 1)
        {
            direction = points[^1] - points[^2];
        }
        else
        {
            direction = points[index + 1] - points[index - 1];
        }

        return direction.Normalized().Perp;
    }

    /// <summary>Adds a semicircular cap around a stroke endpoint.</summary>
    private static void AddCap(List<Vec2> output, Vec2 centre, Vec2 direction, float radius, bool forward)
    {
        if (direction == Vec2.Zero)
        {
            return;
        }

        Vec2 normal = direction.Perp;
        const int steps = 6;

        for (int i = 1; i < steps; i++)
        {
            float t = MathF.PI * i / steps;
            if (forward)
            {
                // From +normal, around the tip in the direction of travel, to -normal.
                output.Add(centre + ((normal * MathF.Cos(t)) + (direction * MathF.Sin(t))) * radius);
            }
            else
            {
                // From -normal, around the tail, back to +normal.
                output.Add(centre + ((normal * -MathF.Cos(t)) - (direction * MathF.Sin(t))) * radius);
            }
        }
    }

    /// <summary>Corner cutting that keeps the two endpoints fixed.</summary>
    private static List<Vec2> Chaikin(List<Vec2> points, int iterations)
    {
        List<Vec2> current = points;
        for (int pass = 0; pass < iterations; pass++)
        {
            if (current.Count < 3)
            {
                return current;
            }

            List<Vec2> next = new((current.Count * 2) - 1) { current[0] };
            for (int i = 0; i + 1 < current.Count; i++)
            {
                Vec2 a = current[i];
                Vec2 b = current[i + 1];
                next.Add((a * 0.75f) + (b * 0.25f));
                if (i + 2 < current.Count)
                {
                    next.Add((a * 0.25f) + (b * 0.75f));
                }
            }

            next.Add(current[^1]);
            current = next;
        }

        return current;
    }

    /// <summary>Ramer–Douglas–Peucker simplification.</summary>
    private static List<Vec2> Simplify(List<Vec2> points, float tolerance)
    {
        if (points.Count < 3)
        {
            return points;
        }

        bool[] keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;

        Stack<(int First, int Last)> stack = new();
        stack.Push((0, points.Count - 1));

        while (stack.Count > 0)
        {
            (int first, int last) = stack.Pop();
            float furthest = 0f;
            int index = -1;

            for (int i = first + 1; i < last; i++)
            {
                float distance = DistanceToSegment(points[i], points[first], points[last]);
                if (distance > furthest)
                {
                    furthest = distance;
                    index = i;
                }
            }

            if (index >= 0 && furthest > tolerance)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }

        List<Vec2> result = new(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    private static float DistanceToSegment(Vec2 point, Vec2 a, Vec2 b)
    {
        Vec2 ab = b - a;
        float lengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (lengthSquared < 1e-6f)
        {
            return point.DistanceTo(a);
        }

        float t = Math.Clamp((((point.X - a.X) * ab.X) + ((point.Y - a.Y) * ab.Y)) / lengthSquared, 0f, 1f);
        return point.DistanceTo(a + (ab * t));
    }

    /// <summary>Rebuilds a polygon as a closed contour with Catmull-Rom handles.</summary>
    private static GlyphContour Smooth(List<Vec2> points)
    {
        GlyphContour contour = new() { Closed = true };
        int count = points.Count;

        for (int i = 0; i < count; i++)
        {
            Vec2 previous = points[(i - 1 + count) % count];
            Vec2 next = points[(i + 1) % count];
            Vec2 tangent = (next - previous) / 6f;
            Vec2 point = points[i];

            contour.Nodes.Add(new GlyphNode
            {
                Point = point,
                In = point - tangent,
                Out = point + tangent,
                HasIn = true,
                HasOut = true,
                Smooth = true,
            });
        }

        float area = 0f;
        for (int i = 0; i < count; i++)
        {
            Vec2 a = points[i];
            Vec2 b = points[(i + 1) % count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        if (area < 0f)
        {
            contour.Reverse();
        }

        return contour;
    }

    /// <summary>A filled circle for a single-dot stroke.</summary>
    private static GlyphContour Disc(Vec2 centre, float radius)
    {
        OutlineBuilder builder = new();
        builder.Disc(centre, MathF.Max(1f, radius));
        return builder.Contours[0].Clone();
    }
}
