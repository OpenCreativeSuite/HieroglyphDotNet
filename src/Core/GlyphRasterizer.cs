using Donkeh.Windowing;

namespace HieroglyphDotNet.Core;

/// <summary>One horizontal run of covered pixels.</summary>
public readonly record struct GlyphRun(int X, int Y, int Width, byte Coverage);

/// <summary>A rasterised outline positioned relative to the glyph origin.</summary>
public sealed class GlyphBitmap
{
    /// <summary>The bitmap width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The bitmap height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>The x of the glyph origin within the bitmap.</summary>
    public required int OriginX { get; init; }

    /// <summary>The y of the baseline within the bitmap.</summary>
    public required int OriginY { get; init; }

    /// <summary>Covered runs in paint order.</summary>
    public required List<GlyphRun> Runs { get; init; }
}

/// <summary>Flattens glyph outlines and rasterises them with antialiased coverage.</summary>
public static class GlyphRasterizer
{
    private const int SubSamples = 4;

    private sealed class Edge(Vec2 a, Vec2 b)
    {
        public float X0 { get; } = a.X;

        public float Y0 { get; } = a.Y;

        public float X1 { get; } = b.X;

        public float Y1 { get; } = b.Y;

        public float YMin { get; } = MathF.Min(a.Y, b.Y);

        public float YMax { get; } = MathF.Max(a.Y, b.Y);

        public int Direction { get; } = b.Y > a.Y ? 1 : -1;

        public float XAt(float y) => X0 + ((y - Y0) * (X1 - X0) / (Y1 - Y0));
    }

    private readonly record struct Crossing(float X, int Direction);

    /// <summary>Flattens one contour into device-space points.</summary>
    public static List<Vec2> Flatten(GlyphContour contour, float scale, float originX, float baselineY, bool forceClose = true)
    {
        List<Vec2> points = [];
        List<GlyphNode> nodes = contour.Nodes;
        if (nodes.Count == 0)
        {
            return points;
        }

        Vec2 Map(Vec2 point) => new(originX + (point.X * scale), baselineY - (point.Y * scale));

        int count = contour.Closed ? nodes.Count : nodes.Count - 1;
        for (int i = 0; i < count; i++)
        {
            GlyphNode from = nodes[i];
            GlyphNode to = nodes[(i + 1) % nodes.Count];
            Vec2 p0 = Map(from.Point);
            Vec2 p3 = Map(to.Point);

            if (points.Count == 0)
            {
                points.Add(p0);
            }

            if (!from.HasOut && !to.HasIn)
            {
                points.Add(p3);
                continue;
            }

            Vec2 p1 = Map(from.HasOut ? from.Out : from.Point);
            Vec2 p2 = Map(to.HasIn ? to.In : to.Point);

            float length = (p1 - p0).Length + (p2 - p1).Length + (p3 - p2).Length;
            int steps = Math.Clamp((int)MathF.Ceiling(length / 2.4f), 2, 64);
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                points.Add(Cubic(p0, p1, p2, p3, t));
            }
        }

        if (!contour.Closed && forceClose && points.Count > 2)
        {
            points.Add(points[0]);
        }

        return points;
    }

    /// <summary>The point on a cubic Bézier at a parameter.</summary>
    public static Vec2 Cubic(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t)
    {
        float u = 1f - t;
        float a = u * u * u;
        float b = 3f * u * u * t;
        float c = 3f * u * t * t;
        float d = t * t * t;
        return new Vec2(
            (a * p0.X) + (b * p1.X) + (c * p2.X) + (d * p3.X),
            (a * p0.Y) + (b * p1.Y) + (c * p2.Y) + (d * p3.Y));
    }

    /// <summary>Rasterises a glyph at a scale of pixels per font unit.</summary>
    public static GlyphBitmap Rasterize(Glyph glyph, float scale, float padPixels = 1f)
    {
        if (!glyph.HasOutline || scale <= 0f)
        {
            return new GlyphBitmap { Width = 1, Height = 1, OriginX = 1, OriginY = 1, Runs = [] };
        }

        Bounds bounds = glyph.OutlineBounds;
        int originX = (int)MathF.Ceiling(-bounds.MinX * scale) + (int)MathF.Ceiling(padPixels);
        int baselineY = (int)MathF.Ceiling(bounds.MaxY * scale) + (int)MathF.Ceiling(padPixels);
        int width = originX + (int)MathF.Ceiling(bounds.MaxX * scale) + (int)MathF.Ceiling(padPixels) + 1;
        int height = baselineY + (int)MathF.Ceiling(-bounds.MinY * scale) + (int)MathF.Ceiling(padPixels) + 1;

        width = Math.Clamp(width, 1, 8192);
        height = Math.Clamp(height, 1, 8192);

        List<Edge> edges = [];
        foreach (GlyphContour contour in glyph.Contours)
        {
            if (!contour.IsDrawable)
            {
                continue;
            }

            List<Vec2> points = Flatten(contour, scale, originX, baselineY);
            if (points.Count < 3)
            {
                continue;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vec2 a = points[i];
                Vec2 b = points[(i + 1) % points.Count];
                if (MathF.Abs(a.Y - b.Y) < 0.0005f)
                {
                    continue;
                }

                edges.Add(new Edge(a, b));
            }
        }

        List<GlyphRun> runs = new(edges.Count * 4);
        if (edges.Count > 0)
        {
            RasterizeEdges(edges, width, height, runs);
        }

        return new GlyphBitmap
        {
            Width = width,
            Height = height,
            OriginX = originX,
            OriginY = baselineY,
            Runs = runs,
        };
    }

    private static void RasterizeEdges(List<Edge> edges, int width, int height, List<GlyphRun> output)
    {
        float[] cover = new float[width + 1];
        List<Crossing> crossings = new(edges.Count);

        for (int row = 0; row < height; row++)
        {
            Array.Clear(cover, 0, cover.Length);

            for (int sample = 0; sample < SubSamples; sample++)
            {
                float y = row + ((sample + 0.5f) / SubSamples);
                crossings.Clear();

                foreach (Edge edge in edges)
                {
                    if (y < edge.YMin || y >= edge.YMax)
                    {
                        continue;
                    }

                    crossings.Add(new Crossing(edge.XAt(y), edge.Direction));
                }

                if (crossings.Count < 2)
                {
                    continue;
                }

                crossings.Sort(static (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Direction.CompareTo(b.Direction));

                int winding = 0;
                float spanStart = 0f;
                bool inside = false;

                foreach (Crossing crossing in crossings)
                {
                    bool wasInside = inside;
                    winding += crossing.Direction;
                    inside = winding != 0;

                    if (!wasInside && inside)
                    {
                        spanStart = crossing.X;
                    }
                    else if (wasInside && !inside)
                    {
                        Accumulate(cover, spanStart, crossing.X, width);
                    }
                }
            }

            EmitRun(output, row, cover, width);
        }
    }

    private static void Accumulate(float[] cover, float x0, float x1, int width)
    {
        x0 = MathF.Max(x0, 0f);
        x1 = MathF.Min(x1, width);
        if (x1 <= x0)
        {
            return;
        }

        int i0 = (int)MathF.Floor(x0);
        int i1 = (int)MathF.Floor(x1);

        if (i0 == i1)
        {
            if (i0 >= 0 && i0 < width)
            {
                cover[i0] += x1 - x0;
            }

            return;
        }

        if (i0 >= 0 && i0 < width)
        {
            cover[i0] += (i0 + 1) - x0;
        }

        int start = Math.Max(i0 + 1, 0);
        int end = Math.Min(i1 - 1, width - 1);
        for (int i = start; i <= end; i++)
        {
            cover[i] += 1f;
        }

        if (i1 >= 0 && i1 < width)
        {
            cover[i1] += x1 - i1;
        }
    }

    private static void EmitRun(List<GlyphRun> output, int row, float[] cover, int width)
    {
        int runStart = -1;
        int runAlpha = 0;

        for (int i = 0; i <= width; i++)
        {
            int alpha = 0;
            if (i < width)
            {
                float coverage = MathF.Min(cover[i], SubSamples) / SubSamples;
                alpha = Math.Clamp((int)((coverage * 255f) + 0.5f), 0, 255);
            }

            if (alpha != runAlpha)
            {
                if (runAlpha > 0 && runStart >= 0)
                {
                    output.Add(new GlyphRun(runStart, row, i - runStart, (byte)runAlpha));
                }

                runStart = i;
                runAlpha = alpha;
            }
        }
    }

    /// <summary>Adds a run list to a canvas, offset and tinted.</summary>
    public static void Paint(CanvasContext context, GlyphBitmap bitmap, float x, float y, uint color)
    {
        uint rgb = color & 0x00FFFFFFu;
        int baseAlpha = (int)(color >> 24);

        foreach (GlyphRun run in bitmap.Runs)
        {
            int alpha = (run.Coverage * baseAlpha) / 255;
            if (alpha <= 0)
            {
                continue;
            }

            context.FillRect(x + run.X, y + run.Y, run.Width, 1f, rgb | ((uint)alpha << 24));
        }
    }
}

/// <summary>Caches rasterised glyphs for the preview and the character browser.</summary>
public sealed class GlyphRasterCache
{
    private readonly Dictionary<(int Code, int Scale, int Width), GlyphBitmap> _bitmaps = [];
    private int _version = -1;

    /// <summary>Drops every bitmap when the geometry version moves on.</summary>
    public void Sync(int version)
    {
        if (version == _version)
        {
            return;
        }

        _version = version;
        _bitmaps.Clear();
    }

    /// <summary>Gets a rasterised glyph at a scale of pixels per font unit.</summary>
    public GlyphBitmap Get(Glyph glyph, float scale)
    {
        int quantised = (int)MathF.Round(scale * 1000f);
        (int, int, int) key = (glyph.Codepoint, quantised, glyph.AdvanceWidth);
        if (_bitmaps.TryGetValue(key, out GlyphBitmap? cached))
        {
            return cached;
        }

        GlyphBitmap bitmap = GlyphRasterizer.Rasterize(glyph, scale);
        _bitmaps[key] = bitmap;
        return bitmap;
    }

    /// <summary>Empties the cache.</summary>
    public void Clear()
    {
        _bitmaps.Clear();
        _version = -1;
    }
}
