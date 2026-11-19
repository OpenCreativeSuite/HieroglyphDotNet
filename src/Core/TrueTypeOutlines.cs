namespace HieroglyphDotNet.Core;

/// <summary>The compiled TrueType outline data plus the metrics it produced.</summary>
internal sealed record TrueTypeResult(
    byte[] Glyf,
    byte[] Loca,
    int[] Advances,
    int[] LeftBearings,
    Bounds TotalBounds,
    short XMin,
    short YMin,
    short YMax);

/// <summary>Converts cubic glyph outlines into TrueType quadratic contours.</summary>
internal static class TrueTypeOutlines
{
    internal readonly record struct Point(Vec2 Position, bool OnCurve);

    /// <summary>Builds glyf and loca for an ordered glyph list where index zero is .notdef.</summary>
    public static TrueTypeResult Build(IReadOnlyList<Glyph> glyphs)
    {
        List<byte[]> data = new(glyphs.Count);
        int[] advances = new int[glyphs.Count];
        int[] leftBearings = new int[glyphs.Count];
        ByteWriter loca = new();
        Bounds total = Bounds.Empty;
        int offset = 0;

        for (int index = 0; index < glyphs.Count; index++)
        {
            Glyph glyph = glyphs[index];
            advances[index] = Math.Max(0, glyph.AdvanceWidth);

            List<List<Point>> contours = [];
            foreach (GlyphContour contour in glyph.Contours)
            {
                if (contour.Nodes.Count < 2)
                {
                    continue;
                }

                List<Point> points = QuadraticContour(contour);
                if (points.Count < 2)
                {
                    continue;
                }

                // TrueType fills clockwise contours, the opposite of the
                // editor's counter-clockwise outer contours.
                points.Reverse();
                contours.Add(points);
            }

            if (contours.Count == 0)
            {
                loca.U32((uint)offset);
                data.Add([]);
                leftBearings[index] = 0;
                continue;
            }

            IEnumerable<Point> all = contours.SelectMany(list => list);
            float minX = all.Min(point => point.Position.X);
            float minY = all.Min(point => point.Position.Y);
            float maxX = all.Max(point => point.Position.X);
            float maxY = all.Max(point => point.Position.Y);

            short xMin = (short)MathF.Floor(minX);
            short yMin = (short)MathF.Floor(minY);
            short xMax = (short)MathF.Ceiling(maxX);
            short yTop = (short)MathF.Ceiling(maxY);

            leftBearings[index] = xMin;
            total = total.Union(new Bounds(minX, minY, maxX, maxY));

            ByteWriter writer = new();
            writer.I16((short)contours.Count);
            writer.I16(xMin);
            writer.I16(yMin);
            writer.I16(xMax);
            writer.I16(yTop);

            int end = -1;
            foreach (List<Point> contour in contours)
            {
                end += contour.Count;
                writer.I16((short)end);
            }

            writer.U16(0);

            // glyf stores all flags first, then all x deltas, then all y
            // deltas. The flags here never set the short-vector bits, so every
            // coordinate is a signed 16-bit delta.
            ByteWriter flags = new();
            ByteWriter xs = new();
            ByteWriter ys = new();
            int lastX = 0;
            int lastY = 0;
            foreach (List<Point> contour in contours)
            {
                foreach (Point point in contour)
                {
                    int x = (int)MathF.Round(point.Position.X);
                    int y = (int)MathF.Round(point.Position.Y);
                    flags.U8((byte)(point.OnCurve ? 0x01 : 0x00));
                    xs.I16((short)Math.Clamp(x - lastX, short.MinValue, short.MaxValue));
                    ys.I16((short)Math.Clamp(y - lastY, short.MinValue, short.MaxValue));
                    lastX = x;
                    lastY = y;
                }
            }

            writer.Bytes(flags.ToArray());
            writer.Bytes(xs.ToArray());
            writer.Bytes(ys.ToArray());

            byte[] bytes = FontBinary.Pad(writer.ToArray());
            loca.U32((uint)offset);
            data.Add(bytes);
            offset += bytes.Length;
        }

        loca.U32((uint)offset);

        ByteWriter glyf = new();
        foreach (byte[] glyph in data)
        {
            glyf.Bytes(glyph);
        }

        short globalYMax = (short)Math.Clamp((int)MathF.Ceiling(total.MaxY), short.MinValue, short.MaxValue);
        return new TrueTypeResult(
            glyf.ToArray(),
            loca.ToArray(),
            advances,
            leftBearings,
            total,
            (short)Math.Clamp((int)MathF.Floor(total.MinX), short.MinValue, short.MaxValue),
            (short)Math.Clamp((int)MathF.Floor(total.MinY), short.MinValue, short.MaxValue),
            globalYMax);
    }

    /// <summary>Approximates one cubic contour with quadratic on and off-curve points.</summary>
    internal static List<Point> QuadraticContour(GlyphContour contour)
    {
        List<Point> points = [];
        List<GlyphNode> nodes = contour.Nodes;
        if (nodes.Count < 2)
        {
            return points;
        }

        points.Add(new Point(nodes[0].Point, true));
        int segments = contour.Closed ? nodes.Count : nodes.Count - 1;

        for (int i = 0; i < segments; i++)
        {
            GlyphNode from = nodes[i];
            GlyphNode to = nodes[(i + 1) % nodes.Count];
            Vec2 p0 = from.Point;
            Vec2 p3 = to.Point;

            if (!from.HasOut && !to.HasIn)
            {
                points.Add(new Point(p3, true));
                continue;
            }

            Vec2 p1 = from.HasOut ? from.Out : p0;
            Vec2 p2 = to.HasIn ? to.In : p3;
            Emit(p0, p1, p2, p3, points, 0);
        }

        if (points.Count > 1 && points[^1].Position == points[0].Position)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static void Emit(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, List<Point> output, int depth)
    {
        Vec2 control = (((p1 + p2) * 3f) - p0 - p3) * 0.25f;
        Vec2 cubicMid = (p0 + (p1 * 3f) + (p2 * 3f) + p3) * 0.125f;
        Vec2 quadMid = (p0 + (control * 2f) + p3) * 0.25f;

        if ((cubicMid - quadMid).Length <= 0.35f || depth >= 6)
        {
            output.Add(new Point(control, false));
            output.Add(new Point(p3, true));
            return;
        }

        Vec2 p01 = Vec2.Lerp(p0, p1, 0.5f);
        Vec2 p12 = Vec2.Lerp(p1, p2, 0.5f);
        Vec2 p23 = Vec2.Lerp(p2, p3, 0.5f);
        Vec2 p012 = Vec2.Lerp(p01, p12, 0.5f);
        Vec2 p123 = Vec2.Lerp(p12, p23, 0.5f);
        Vec2 mid = Vec2.Lerp(p012, p123, 0.5f);

        Emit(p0, p01, p012, mid, output, depth + 1);
        Emit(mid, p123, p23, p3, output, depth + 1);
    }
}
