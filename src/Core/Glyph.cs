namespace HieroglyphDotNet.Core;

/// <summary>One on-curve point with two optional cubic control handles.</summary>
public sealed class GlyphNode
{
    /// <summary>The on-curve position.</summary>
    public Vec2 Point { get; set; }

    /// <summary>The control point of the segment arriving at this node.</summary>
    public Vec2 In { get; set; }

    /// <summary>The control point of the segment leaving this node.</summary>
    public Vec2 Out { get; set; }

    /// <summary>Whether the incoming segment is curved.</summary>
    public bool HasIn { get; set; }

    /// <summary>Whether the outgoing segment is curved.</summary>
    public bool HasOut { get; set; }

    /// <summary>Whether the two handles are mirrored through the point.</summary>
    public bool Smooth { get; set; }

    /// <summary>Whether this node starts or ends a curved segment.</summary>
    public bool IsCurved => HasIn || HasOut;

    /// <summary>Creates a corner node.</summary>
    public static GlyphNode Corner(Vec2 point) => new() { Point = point };

    /// <summary>Creates a smooth node whose handles follow a direction.</summary>
    public static GlyphNode SmoothAt(Vec2 point, Vec2 direction, float inLength, float outLength)
    {
        Vec2 unit = direction.Normalized();
        return new GlyphNode
        {
            Point = point,
            In = point - (unit * inLength),
            Out = point + (unit * outLength),
            HasIn = inLength > 0f,
            HasOut = outLength > 0f,
            Smooth = true,
        };
    }

    /// <summary>Copies the node.</summary>
    public GlyphNode Clone() => new()
    {
        Point = Point,
        In = In,
        Out = Out,
        HasIn = HasIn,
        HasOut = HasOut,
        Smooth = Smooth,
    };

    /// <summary>Moves the node and both handles.</summary>
    public void Translate(Vec2 delta)
    {
        Point += delta;
        In += delta;
        Out += delta;
    }

    /// <summary>Mirrors a moved outgoing handle onto the incoming one.</summary>
    public void MirrorOut()
    {
        if (!Smooth || !HasOut)
        {
            return;
        }

        In = Point - (Out - Point);
        HasIn = true;
    }

    /// <summary>Mirrors a moved incoming handle onto the outgoing one.</summary>
    public void MirrorIn()
    {
        if (!Smooth || !HasIn)
        {
            return;
        }

        Out = Point - (In - Point);
        HasOut = true;
    }
}

/// <summary>A closed or open run of nodes.</summary>
public sealed class GlyphContour
{
    /// <summary>The nodes in drawing order.</summary>
    public List<GlyphNode> Nodes { get; set; } = [];

    /// <summary>Whether the last node joins the first.</summary>
    public bool Closed { get; set; } = true;

    /// <summary>A contour with nothing in it.</summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>Whether there are enough nodes to describe an outline.</summary>
    public bool IsDrawable => Nodes.Count >= 2;

    /// <summary>Copies the contour and every node.</summary>
    public GlyphContour Clone() => new()
    {
        Closed = Closed,
        Nodes = Nodes.Select(node => node.Clone()).ToList(),
    };

    /// <summary>The bounding box of the control points.</summary>
    public Bounds Bounds
    {
        get
        {
            Bounds bounds = Bounds.Empty;
            foreach (GlyphNode node in Nodes)
            {
                bounds = bounds.With(node.Point);
                if (node.HasIn)
                {
                    bounds = bounds.With(node.In);
                }

                if (node.HasOut)
                {
                    bounds = bounds.With(node.Out);
                }
            }

            return bounds;
        }
    }

    /// <summary>Reverses the node order and swaps each node's handles.</summary>
    public void Reverse()
    {
        Nodes.Reverse();
        foreach (GlyphNode node in Nodes)
        {
            (node.In, node.Out) = (node.Out, node.In);
            (node.HasIn, node.HasOut) = (node.HasOut, node.HasIn);
        }
    }
}

/// <summary>One character's outline, advance and metadata.</summary>
public sealed class Glyph
{
    /// <summary>The Unicode code point.</summary>
    public int Codepoint { get; set; }

    /// <summary>A PostScript-style name such as "A" or "one".</summary>
    public string Name { get; set; } = "";

    /// <summary>The advance width in font units.</summary>
    public int AdvanceWidth { get; set; } = 600;

    /// <summary>The outlined contours.</summary>
    public List<GlyphContour> Contours { get; set; } = [];

    /// <summary>The character this glyph represents.</summary>
    public string Character => Codepoint is > 0 and <= 0x10FFFF
        ? char.ConvertFromUtf32(Codepoint)
        : "";

    /// <summary>Whether any contour has been drawn.</summary>
    public bool HasOutline => Contours.Any(contour => contour.IsDrawable);

    /// <summary>Whether the glyph should be exported.</summary>
    public bool IsBlank => !HasOutline;

    /// <summary>Whether the code point needs a second UTF-16 unit.</summary>
    public bool IsSurrogate => Codepoint > 0xFFFF;

    /// <summary>The total number of editable nodes.</summary>
    public int NodeCount => Contours.Sum(contour => contour.Nodes.Count);

    /// <summary>A tight bounding box of the on-curve points, or an empty box.</summary>
    public Bounds OutlineBounds
    {
        get
        {
            Bounds bounds = Bounds.Empty;
            foreach (GlyphContour contour in Contours)
            {
                if (!contour.IsDrawable)
                {
                    continue;
                }

                bounds = bounds.Union(contour.Bounds);
            }

            return bounds;
        }
    }

    /// <summary>The leftmost point of the outline, or zero for a blank glyph.</summary>
    public float LeftEdge => HasOutline ? OutlineBounds.MinX : 0f;

    /// <summary>The rightmost point of the outline, or zero for a blank glyph.</summary>
    public float RightEdge => HasOutline ? OutlineBounds.MaxX : 0f;

    /// <summary>The gap between the origin and the leftmost point.</summary>
    public int LeftSideBearing => HasOutline ? (int)MathF.Round(LeftEdge) : 0;

    /// <summary>The gap between the rightmost point and the advance width.</summary>
    public int RightSideBearing => HasOutline ? AdvanceWidth - (int)MathF.Round(RightEdge) : AdvanceWidth;

    /// <summary>An empty glyph for a code point.</summary>
    public static Glyph Blank(int codepoint, string name, int advance) => new()
    {
        Codepoint = codepoint,
        Name = name,
        AdvanceWidth = advance,
    };

    /// <summary>A deep copy.</summary>
    public Glyph Clone() => new()
    {
        Codepoint = Codepoint,
        Name = Name,
        AdvanceWidth = AdvanceWidth,
        Contours = Contours.Select(contour => contour.Clone()).ToList(),
    };

    /// <summary>A copy carrying a different code point and name.</summary>
    public Glyph CloneFor(int codepoint, string name, int advance)
    {
        Glyph clone = Clone();
        clone.Codepoint = codepoint;
        clone.Name = name;
        clone.AdvanceWidth = advance;
        return clone;
    }

    /// <summary>Moves every node.</summary>
    public void Translate(Vec2 delta)
    {
        foreach (GlyphContour contour in Contours)
        {
            foreach (GlyphNode node in contour.Nodes)
            {
                node.Translate(delta);
            }
        }
    }

    /// <summary>Scales every node around a pivot.</summary>
    public void Scale(Vec2 pivot, float sx, float sy)
    {
        Vec2 Map(Vec2 point) => new(
            pivot.X + ((point.X - pivot.X) * sx),
            pivot.Y + ((point.Y - pivot.Y) * sy));

        foreach (GlyphContour contour in Contours)
        {
            foreach (GlyphNode node in contour.Nodes)
            {
                node.Point = Map(node.Point);
                node.In = Map(node.In);
                node.Out = Map(node.Out);
            }
        }
    }

    /// <summary>Mirrors every node horizontally around a pivot x.</summary>
    public void FlipHorizontal(float pivot)
    {
        foreach (GlyphContour contour in Contours)
        {
            foreach (GlyphNode node in contour.Nodes)
            {
                node.Point = new Vec2((2f * pivot) - node.Point.X, node.Point.Y);
                node.In = new Vec2((2f * pivot) - node.In.X, node.In.Y);
                node.Out = new Vec2((2f * pivot) - node.Out.X, node.Out.Y);
                (node.In, node.Out) = (node.Out, node.In);
                (node.HasIn, node.HasOut) = (node.HasOut, node.HasIn);
            }

            contour.Reverse();
        }
    }

    /// <summary>Mirrors every node vertically around a pivot y.</summary>
    public void FlipVertical(float pivot)
    {
        foreach (GlyphContour contour in Contours)
        {
            foreach (GlyphNode node in contour.Nodes)
            {
                node.Point = new Vec2(node.Point.X, (2f * pivot) - node.Point.Y);
                node.In = new Vec2(node.In.X, (2f * pivot) - node.In.Y);
                node.Out = new Vec2(node.Out.X, (2f * pivot) - node.Out.Y);
                (node.In, node.Out) = (node.Out, node.In);
                (node.HasIn, node.HasOut) = (node.HasOut, node.HasIn);
            }

            contour.Reverse();
        }
    }

    /// <summary>Rotates every node by a quarter turn around a pivot.</summary>
    public void RotateQuarter(Vec2 pivot, bool clockwise)
    {
        float angle = clockwise ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
        foreach (GlyphContour contour in Contours)
        {
            foreach (GlyphNode node in contour.Nodes)
            {
                node.Point = node.Point.Rotated(pivot, angle);
                node.In = node.In.Rotated(pivot, angle);
                node.Out = node.Out.Rotated(pivot, angle);
            }
        }
    }
}
