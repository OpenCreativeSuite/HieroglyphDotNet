namespace HieroglyphDotNet.Core;

/// <summary>Pointer, pen and keyboard interaction for the glyph canvas.</summary>
public sealed partial class FontEditor
{
    private enum HitKind
    {
        None,
        Node,
        HandleIn,
        HandleOut,
        Contour,
        Advance,
    }

    private readonly record struct Hit(HitKind Kind, int Contour, int Node);

    private enum DragMode
    {
        None,
        Nodes,
        Shape,
        HandleIn,
        HandleOut,
        Marquee,
        Pan,
        Advance,
        PenHandle,
        Brush,
    }

    private DragMode _dragMode;
    private Hit _dragHit;
    private Vec2 _dragStartUnits;
    private Vec2 _dragLastUnits;
    private Vec2 _dragStartScreen;
    private Vec2 _marqueeEndScreen;
    private GlyphNode? _dragOriginalNode;
    private Glyph? _dragOriginalShape;
    private readonly List<(int Index, GlyphNode Node)> _dragNodes = [];
    private readonly List<Vec2> _brushPoints = [];
    private Glyph? _brushPreview;
    private int _penContour = -1;
    private int _penNode = -1;

    /// <summary>The points collected so far in the current brush stroke.</summary>
    public IReadOnlyList<Vec2> BrushPoints => _brushPoints;

    /// <summary>The live, fully shaped stroke shown while dragging the brush.</summary>
    public Glyph? BrushPreview => _brushPreview;

    /// <summary>Whether a pen stroke is being built.</summary>
    public bool IsPenActive => _penContour >= 0;

    /// <summary>The current marquee in canvas pixels, or null.</summary>
    public (Vec2 Start, Vec2 End)? Marquee => _dragMode == DragMode.Marquee ? (_dragStartScreen, _marqueeEndScreen) : null;

    /// <summary>Whether the pointer is dragging something.</summary>
    public bool IsDragging => _dragMode != DragMode.None;

    /// <summary>The node the pen will connect to, for the rubber-band preview.</summary>
    public Vec2? PenPreview
    {
        get
        {
            Glyph? glyph = SelectedGlyph;
            if (glyph is null || _penContour < 0 || _penContour >= glyph.Contours.Count)
            {
                return null;
            }

            GlyphContour contour = glyph.Contours[_penContour];
            return contour.Nodes.Count > 0 ? contour.Nodes[^1].Point : null;
        }
    }

    // -------------------------------------------------------------- pointer

    /// <summary>Handles a primary pointer press on the canvas.</summary>
    public void PointerDown(Vec2 screen, bool shift, bool alt, bool middle)
    {
        ShowStarterPanel = false;
        _dragStartScreen = screen;
        _marqueeEndScreen = screen;
        _dragStartUnits = View.ToUnits(screen);
        _dragLastUnits = _dragStartUnits;
        _dragHit = HitTest(screen);

        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        if (middle || Tool == GlyphTool.Hand)
        {
            _dragMode = DragMode.Pan;
            Notify();
            return;
        }

        if (Tool == GlyphTool.Zoom)
        {
            ZoomAt(alt ? 0.7f : 1.4f, screen);
            return;
        }

        if (Tool == GlyphTool.Pen)
        {
            PenDown(screen, shift, alt);
            Notify();
            return;
        }

        if (Tool == GlyphTool.Brush)
        {
            StartBrush(_dragStartUnits);
            return;
        }

        if (Tool == GlyphTool.Select)
        {
            if (_dragHit.Kind == HitKind.Advance)
            {
                PushUndo();
                _dragMode = DragMode.Advance;
                Notify();
                return;
            }

            if (_dragHit.Kind is HitKind.Node or HitKind.HandleIn or HitKind.HandleOut)
            {
                // Selecting a node from the selection tool switches to node
                // editing, which is what a beginner expects from a click.
                Tool = GlyphTool.Node;
                _dragHit = HitTest(screen);
            }
        }

        if (Tool == GlyphTool.Node)
        {
            switch (_dragHit.Kind)
            {
                case HitKind.HandleIn:
                case HitKind.HandleOut:
                    PushUndo();
                    SelectNode(_dragHit.Contour, _dragHit.Node, shift);
                    _dragMode = _dragHit.Kind == HitKind.HandleIn ? DragMode.HandleIn : DragMode.HandleOut;
                    _dragOriginalNode = glyph.Contours[_dragHit.Contour].Nodes[_dragHit.Node].Clone();
                    if (alt)
                    {
                        GlyphNode node = glyph.Contours[_dragHit.Contour].Nodes[_dragHit.Node];
                        node.Smooth = false;
                    }

                    UpdateStatus();
                    return;

                case HitKind.Node:
                    PushUndo();
                    bool already = _dragHit.Contour == SelectedContour && SelectedNodes.Contains(_dragHit.Node);
                    if (!already)
                    {
                        SelectNode(_dragHit.Contour, _dragHit.Node, shift);
                    }

                    CaptureDragNodes(glyph);
                    _dragMode = DragMode.Nodes;
                    UpdateStatus();
                    return;

                case HitKind.Contour:
                    if (shift)
                    {
                        SelectNode(_dragHit.Contour, -1, true);
                    }
                    else
                    {
                        SelectedContour = _dragHit.Contour;
                        SelectedNodes.Clear();
                    }

                    PushUndo();
                    CaptureDragNodes(glyph);
                    _dragMode = DragMode.Nodes;
                    Notify();
                    return;
            }

            ClearSelection();
            _dragMode = DragMode.Marquee;
            Notify();
            return;
        }

        // Selection tool on empty space or the shape itself.
        if (_dragHit.Kind == HitKind.Contour)
        {
            PushUndo();
            _dragOriginalShape = glyph.Clone();
            _dragMode = DragMode.Shape;
            Notify();
            return;
        }

        _dragMode = DragMode.Marquee;
        ClearSelection();
        Notify();
    }

    /// <summary>Handles pointer movement.</summary>
    public void PointerMove(Vec2 screen, bool shift)
    {
        Vec2 units = View.ToUnits(screen);
        CursorUnits = units;
        CursorOnCanvas = true;
        Glyph? glyph = SelectedGlyph;

        switch (_dragMode)
        {
            case DragMode.None:
                Notify();
                return;

            case DragMode.Pan:
                PanX += screen.X - _dragStartScreen.X;
                PanY += screen.Y - _dragStartScreen.Y;
                _dragStartScreen = screen;
                Notify();
                return;

            case DragMode.Advance:
                if (glyph is not null)
                {
                    int width = Math.Clamp((int)MathF.Round(units.X), 0, 4000);
                    if (width != glyph.AdvanceWidth)
                    {
                        glyph.AdvanceWidth = width;
                        _geometryVersion++;
                        Project.Dirty = true;
                        Notify();
                    }
                }

                return;

            case DragMode.Nodes:
                DragNodes(units, shift);
                return;

            case DragMode.Shape:
                DragShape(units, shift);
                return;

            case DragMode.HandleIn:
            case DragMode.HandleOut:
                DragHandle(units, shift);
                return;

            case DragMode.Marquee:
                _marqueeEndScreen = screen;
                Notify();
                return;

            case DragMode.PenHandle:
                DragPenHandle(units);
                return;

            case DragMode.Brush:
                ContinueBrush(units);
                return;
        }
    }

    /// <summary>Handles pointer release.</summary>
    public void PointerUp(Vec2 screen)
    {
        Glyph? glyph = SelectedGlyph;

        if (_dragMode == DragMode.Marquee && glyph is not null)
        {
            Vec2 a = View.ToUnits(_dragStartScreen);
            Vec2 b = View.ToUnits(new Vec2(_marqueeEndScreen.X, _marqueeEndScreen.Y));
            Bounds box = new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
            if (box.Width * Zoom > 4f || box.Height * Zoom > 4f)
            {
                SelectedContour = -1;
                SelectedNodes.Clear();
                for (int c = 0; c < glyph.Contours.Count; c++)
                {
                    GlyphContour contour = glyph.Contours[c];
                    for (int n = 0; n < contour.Nodes.Count; n++)
                    {
                        if (box.Contains(contour.Nodes[n].Point))
                        {
                            if (SelectedContour < 0)
                            {
                                SelectedContour = c;
                            }

                            if (c == SelectedContour)
                            {
                                SelectedNodes.Add(n);
                            }
                        }
                    }
                }
            }
        }

        if (_dragMode == DragMode.PenHandle)
        {
            Glyph? active = SelectedGlyph;
            if (active is not null && _penContour >= 0 && _penContour < active.Contours.Count)
            {
                GlyphContour contour = active.Contours[_penContour];
                if (_penNode >= 0 && _penNode < contour.Nodes.Count)
                {
                    GlyphNode node = contour.Nodes[_penNode];
                    if ((node.Out - node.Point).Length * Zoom < 3f)
                    {
                        node.HasOut = false;
                        node.HasIn = false;
                        node.Smooth = false;
                    }
                }
            }
        }

        if (_dragMode == DragMode.Brush)
        {
            CommitBrush();
            _dragMode = DragMode.None;
            _dragOriginalNode = null;
            _dragOriginalShape = null;
            _penNode = -1;
            Notify();
            return;
        }

        _dragMode = DragMode.None;
        _dragOriginalNode = null;
        _dragOriginalShape = null;
        _penNode = -1;
        UpdateStatus();
        Notify();
    }

    /// <summary>Ends a drag when the pointer leaves the canvas.</summary>
    public void PointerLeave()
    {
        CursorOnCanvas = false;
        if (!IsDragging)
        {
            Notify();
        }
    }

    /// <summary>Handles a wheel scroll over the canvas.</summary>
    public void Scroll(float deltaX, float deltaY, Vec2 screen, bool control, bool shift)
    {
        if (control || MathF.Abs(deltaY) > MathF.Abs(deltaX))
        {
            float factor = deltaY > 0 ? 1.12f : 0.89f;
            ZoomAt(factor, screen);
            return;
        }

        PanBy(-deltaX, 0f);
    }

    // ------------------------------------------------------------------ pen

    private void PenDown(Vec2 screen, bool shift, bool alt)
    {
        Glyph glyph = SelectedGlyph!;
        Vec2 units = View.ToUnits(screen);
        if (shift)
        {
            units = new Vec2(MathF.Round(units.X / 10f) * 10f, MathF.Round(units.Y / 10f) * 10f);
        }

        float tolerance = 10f / MathF.Max(Zoom, 0.001f);
        bool hasActive = _penContour >= 0 && _penContour < glyph.Contours.Count;

        if (!hasActive)
        {
            PushUndo();
            GlyphContour contour = new() { Closed = false };
            contour.Nodes.Add(GlyphNode.Corner(units));
            glyph.Contours.Add(contour);
            _penContour = glyph.Contours.Count - 1;
            _penNode = 0;
            _dragMode = DragMode.PenHandle;
            SelectedContour = _penContour;
            SelectedNodes.Clear();
            SelectedNodes.Add(0);
            _geometryVersion++;
            Project.Dirty = true;
            return;
        }

        GlyphContour active = glyph.Contours[_penContour];
        if (active.Nodes.Count >= 2 && active.Nodes[0].Point.DistanceTo(units) <= tolerance)
        {
            active.Closed = true;
            FinishPen(close: true, silent: true);
            return;
        }

        PushUndo();
        GlyphNode node = GlyphNode.Corner(units);
        active.Nodes.Add(node);
        _penNode = active.Nodes.Count - 1;
        _dragMode = DragMode.PenHandle;
        SelectedNodes.Clear();
        SelectedNodes.Add(_penNode);
        _geometryVersion++;
        Project.Dirty = true;
    }

    private void DragPenHandle(Vec2 units)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || _penContour < 0 || _penContour >= glyph.Contours.Count)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[_penContour];
        if (_penNode < 0 || _penNode >= contour.Nodes.Count)
        {
            return;
        }

        GlyphNode node = contour.Nodes[_penNode];
        Vec2 delta = units - node.Point;
        if (delta.Length * Zoom < 4f)
        {
            node.HasIn = false;
            node.HasOut = false;
            node.Smooth = false;
        }
        else
        {
            node.Out = units;
            node.HasOut = true;
            node.In = node.Point - delta;
            node.HasIn = true;
            node.Smooth = true;
        }

        _geometryVersion++;
        Project.Dirty = true;
        Notify();
    }

    /// <summary>Ends the current pen contour.</summary>
    public void FinishPen(bool close, bool silent = false)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is not null && _penContour >= 0 && _penContour < glyph.Contours.Count)
        {
            GlyphContour contour = glyph.Contours[_penContour];
            if (close && contour.Nodes.Count >= 3)
            {
                contour.Closed = true;
            }
            else if (contour.Nodes.Count < 2)
            {
                glyph.Contours.RemoveAt(_penContour);
            }
            else
            {
                contour.Closed = false;
            }

            if (contour.Closed && contour.Nodes.Count >= 3)
            {
                // A closed contour that came from the pen may have been wound
                // either way; the renderer uses the non-zero rule, so reverse
                // it when it would become a hole by accident.
                NormaliseDirection(contour);
            }

            _geometryVersion++;
            Project.Dirty = true;
        }

        _penContour = -1;
        _penNode = -1;
        if (_dragMode == DragMode.PenHandle)
        {
            _dragMode = DragMode.None;
        }

        if (!silent)
        {
            UpdateStatus();
            Notify();
        }
    }

    private static void NormaliseDirection(GlyphContour contour)
    {
        float area = 0f;
        for (int i = 0; i < contour.Nodes.Count; i++)
        {
            Vec2 a = contour.Nodes[i].Point;
            Vec2 b = contour.Nodes[(i + 1) % contour.Nodes.Count].Point;
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        if (area < 0f)
        {
            contour.Reverse();
        }
    }

    // --------------------------------------------------------------- dragging

    private void CaptureDragNodes(Glyph glyph)
    {
        _dragNodes.Clear();
        if (SelectedContour < 0 || SelectedContour >= glyph.Contours.Count)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[SelectedContour];
        foreach (int index in SelectedNodes)
        {
            if (index >= 0 && index < contour.Nodes.Count)
            {
                _dragNodes.Add((index, contour.Nodes[index].Clone()));
            }
        }
    }

    private void DragNodes(Vec2 units, bool shift)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || SelectedContour < 0 || SelectedContour >= glyph.Contours.Count)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[SelectedContour];
        Vec2 delta = units - _dragStartUnits;
        if (shift)
        {
            delta = MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? new Vec2(delta.X, 0f) : new Vec2(0f, delta.Y);
        }

        foreach ((int index, GlyphNode original) in _dragNodes)
        {
            if (index >= 0 && index < contour.Nodes.Count)
            {
                GlyphNode node = contour.Nodes[index];
                node.Point = original.Point + delta;
                node.In = original.In + delta;
                node.Out = original.Out + delta;
            }
        }

        _geometryVersion++;
        Project.Dirty = true;
        UpdateStatus();
        Notify();
    }

    private void DragShape(Vec2 units, bool shift)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || _dragOriginalShape is null)
        {
            return;
        }

        Vec2 delta = units - _dragStartUnits;
        if (shift)
        {
            delta = MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? new Vec2(delta.X, 0f) : new Vec2(0f, delta.Y);
        }

        glyph.Contours = _dragOriginalShape.Contours.Select(contour => contour.Clone()).ToList();
        glyph.Translate(delta);
        _geometryVersion++;
        Project.Dirty = true;
        Notify();
    }

    private void DragHandle(Vec2 units, bool shift)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || _dragHit.Contour < 0 || _dragHit.Contour >= glyph.Contours.Count)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[_dragHit.Contour];
        if (_dragHit.Node < 0 || _dragHit.Node >= contour.Nodes.Count)
        {
            return;
        }

        GlyphNode node = contour.Nodes[_dragHit.Node];
        if (shift)
        {
            units = new Vec2(MathF.Round(units.X / 10f) * 10f, MathF.Round(units.Y / 10f) * 10f);
        }

        if (_dragMode == DragMode.HandleOut)
        {
            node.Out = units;
            node.HasOut = true;
            node.MirrorOut();
        }
        else
        {
            node.In = units;
            node.HasIn = true;
            node.MirrorIn();
        }

        _geometryVersion++;
        Project.Dirty = true;
        Notify();
    }

    // ------------------------------------------------------------ hit testing

    /// <summary>Finds what is under a canvas point.</summary>
    public (int Contour, int Node, int Handle) DescribeHit(Vec2 screen)
    {
        Hit hit = HitTest(screen);
        return hit.Kind switch
        {
            HitKind.Node => (hit.Contour, hit.Node, 0),
            HitKind.HandleIn => (hit.Contour, hit.Node, -1),
            HitKind.HandleOut => (hit.Contour, hit.Node, 1),
            HitKind.Contour => (hit.Contour, -1, 0),
            _ => (-1, -1, 0),
        };
    }

    private Hit HitTest(Vec2 screen)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return new Hit(HitKind.None, -1, -1);
        }

        Vec2 units = View.ToUnits(screen);
        float tolerance = 9f / MathF.Max(Zoom, 0.001f);

        // Handles on the selected node win over everything else.
        if (SelectedContour >= 0 && SelectedContour < glyph.Contours.Count && SelectedNodes.Count > 0)
        {
            GlyphContour contour = glyph.Contours[SelectedContour];
            int index = SelectedNode;
            if (index >= 0 && index < contour.Nodes.Count)
            {
                GlyphNode node = contour.Nodes[index];
                if (node.HasOut && node.Out.DistanceTo(units) <= tolerance)
                {
                    return new Hit(HitKind.HandleOut, SelectedContour, index);
                }

                if (node.HasIn && node.In.DistanceTo(units) <= tolerance)
                {
                    return new Hit(HitKind.HandleIn, SelectedContour, index);
                }
            }
        }

        for (int c = 0; c < glyph.Contours.Count; c++)
        {
            GlyphContour contour = glyph.Contours[c];
            for (int n = 0; n < contour.Nodes.Count; n++)
            {
                GlyphNode node = contour.Nodes[n];
                if (node.Point.DistanceTo(units) <= tolerance)
                {
                    return new Hit(HitKind.Node, c, n);
                }

                if (node.HasOut && node.Out.DistanceTo(units) <= tolerance && c == SelectedContour)
                {
                    return new Hit(HitKind.HandleOut, c, n);
                }

                if (node.HasIn && node.In.DistanceTo(units) <= tolerance && c == SelectedContour)
                {
                    return new Hit(HitKind.HandleIn, c, n);
                }
            }
        }

        // The advance-width line is a wide, forgiving target.
        if (MathF.Abs(units.X - glyph.AdvanceWidth) * Zoom <= 6f)
        {
            if (units.Y >= Style.Metrics.Descender - 40 && units.Y <= Style.Metrics.Ascender + 120)
            {
                return new Hit(HitKind.Advance, -1, -1);
            }
        }

        for (int c = 0; c < glyph.Contours.Count; c++)
        {
            if (NearContour(glyph.Contours[c], units, tolerance))
            {
                return new Hit(HitKind.Contour, c, -1);
            }
        }

        return new Hit(HitKind.None, -1, -1);
    }

    private static bool NearContour(GlyphContour contour, Vec2 point, float tolerance)
    {
        List<GlyphNode> nodes = contour.Nodes;
        if (nodes.Count < 2)
        {
            return false;
        }

        int segments = contour.Closed ? nodes.Count : nodes.Count - 1;
        float limit = tolerance * tolerance;

        for (int i = 0; i < segments; i++)
        {
            GlyphNode from = nodes[i];
            GlyphNode to = nodes[(i + 1) % nodes.Count];
            Vec2 p0 = from.Point;
            Vec2 p3 = to.Point;

            if (!from.HasOut && !to.HasIn)
            {
                if (DistanceToSegment(point, p0, p3) <= limit)
                {
                    return true;
                }

                continue;
            }

            Vec2 p1 = from.HasOut ? from.Out : p0;
            Vec2 p2 = to.HasIn ? to.In : p3;
            for (int step = 0; step <= 12; step++)
            {
                Vec2 a = GlyphRasterizer.Cubic(p0, p1, p2, p3, step / 12f);
                Vec2 b = GlyphRasterizer.Cubic(p0, p1, p2, p3, MathF.Min(1f, (step + 1) / 12f));
                if (DistanceToSegment(point, a, b) <= limit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float DistanceToSegment(Vec2 point, Vec2 a, Vec2 b)
    {
        Vec2 ab = b - a;
        float lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
        if (lengthSquared < 1e-6f)
        {
            return point.DistanceSquared(a);
        }

        float t = Math.Clamp(((point.X - a.X) * ab.X + (point.Y - a.Y) * ab.Y) / lengthSquared, 0f, 1f);
        Vec2 closest = a + (ab * t);
        return point.DistanceSquared(closest);
    }

    // ----------------------------------------------------------------- brush

    /// <summary>Whether a brush stroke is being dragged right now.</summary>
    public bool IsBrushActive => _dragMode == DragMode.Brush;

    private void StartBrush(Vec2 units)
    {
        if (SelectedGlyph is null)
        {
            return;
        }

        PushUndo();
        _brushPoints.Clear();
        _brushPoints.Add(units);
        _brushPreview = BuildBrushPreview();
        _dragMode = DragMode.Brush;
        ClearSelection();
        _geometryVersion++;
        Project.Dirty = true;
        Notify();
    }

    private void ContinueBrush(Vec2 units)
    {
        if (_brushPoints.Count > 0)
        {
            float spacing = MathF.Max(0.8f, 1.1f / MathF.Max(Zoom, 0.05f));
            if (_brushPoints[^1].DistanceTo(units) < spacing)
            {
                return;
            }
        }

        _brushPoints.Add(units);
        _brushPreview = BuildBrushPreview();
        _geometryVersion++;
        Notify();
    }

    private Glyph? BuildBrushPreview()
    {
        if (_brushPoints.Count == 0)
        {
            return null;
        }

        List<GlyphContour> contours = StrokeOutline.Build(_brushPoints, BrushWidth);
        if (contours.Count == 0)
        {
            return null;
        }

        return new Glyph
        {
            Codepoint = -1,
            Name = "brush-preview",
            AdvanceWidth = SelectedGlyph?.AdvanceWidth ?? 600,
            Contours = contours,
        };
    }

    private void CommitBrush()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is not null && _brushPoints.Count > 0)
        {
            List<GlyphContour> contours = StrokeOutline.Build(_brushPoints, BrushWidth);
            if (contours.Count > 0)
            {
                int first = glyph.Contours.Count;
                glyph.Contours.AddRange(contours);
                SelectedContour = first;
                SelectedNodes.Clear();
                Project.Dirty = true;
                _geometryVersion++;
                ShowStarterPanel = false;
                NotifyStatus($"Brush stroke added · {BrushWidth:0} units wide");
            }
        }

        _brushPoints.Clear();
        _brushPreview = null;
        if (_dragMode == DragMode.Brush)
        {
            _dragMode = DragMode.None;
        }

        Notify();
    }

    /// <summary>Discards the stroke in progress.</summary>
    public void CancelBrush()
    {
        if (_dragMode != DragMode.Brush && _brushPoints.Count == 0)
        {
            return;
        }

        _brushPoints.Clear();
        _brushPreview = null;
        _dragMode = DragMode.None;
        Notify();
    }

    // -------------------------------------------------------------- keyboard

    /// <summary>Handles a keyboard chord reported by the host.</summary>
    public bool HandleKey(string chord)
    {
        if (IsTyping)
        {
            return false;
        }

        bool shift = chord.Contains("Shift+", StringComparison.Ordinal);
        bool control = chord.Contains("Ctrl+", StringComparison.Ordinal);
        bool alt = chord.Contains("Alt+", StringComparison.Ordinal);
        string key = chord
            .Replace("Shift+", string.Empty, StringComparison.Ordinal)
            .Replace("Ctrl+", string.Empty, StringComparison.Ordinal)
            .Replace("Alt+", string.Empty, StringComparison.Ordinal)
            .Replace("Control+", string.Empty, StringComparison.Ordinal);

        if (key is "Number0" or "Number1" or "Number2")
        {
            key = key[6..];
        }

        if (control)
        {
            switch (key)
            {
                case "Z":
                    if (shift)
                    {
                        Redo();
                    }
                    else
                    {
                        Undo();
                    }

                    return true;
                case "Y":
                    Redo();
                    return true;
                case "C":
                    CopyGlyph();
                    return true;
                case "V":
                    PasteGlyph();
                    return true;
                case "D":
                    OpenDuplicateDialog();
                    return true;
                case "A":
                    SelectAllNodes();
                    return true;
                case "0":
                    FitGlyph();
                    return true;
                case "Equals":
                case "Plus":
                    ZoomStep(1);
                    return true;
                case "Minus":
                    ZoomStep(-1);
                    return true;
                case "E":
                    OpenExport();
                    return true;
                case "S":
                    SaveProject();
                    return true;
                case "N":
                    OpenNewFont();
                    return true;
                case "O":
                    OpenProject();
                    return true;
            }

            return false;
        }

        switch (key)
        {
            case "V":
                SetTool(GlyphTool.Select);
                return true;
            case "A":
                SetTool(GlyphTool.Node);
                return true;
            case "P":
                SetTool(GlyphTool.Pen);
                return true;
            case "B":
                SetTool(GlyphTool.Brush);
                return true;
            case "H":
                SetTool(GlyphTool.Hand);
                return true;
            case "Z":
                SetTool(GlyphTool.Zoom);
                return true;
            case "Delete":
            case "Backspace":
                DeleteSelection();
                return true;
            case "Escape":
                if (OpenMenu is not null)
                {
                    CloseMenu();
                }
                else if (ShowFileBrowser)
                {
                    CloseFileBrowser();
                }
                else if (IsPenActive)
                {
                    FinishPen(close: false);
                }
                else if (IsBrushActive)
                {
                    CancelBrush();
                }
                else
                {
                    ClearSelection();
                    Notify();
                }

                return true;
            case "Enter":
            case "Return":
                if (IsPenActive)
                {
                    FinishPen(close: false);
                    return true;
                }

                if (ShowFileBrowser)
                {
                    BrowserConfirmEntry();
                    return true;
                }

                return false;
            case "PageUp":
                StepGlyph(-1);
                return true;
            case "PageDown":
                StepGlyph(1);
                return true;
            case "ArrowLeft":
            case "Left":
                Nudge(new Vec2(shift ? -1f : -10f, 0f));
                return true;
            case "ArrowRight":
            case "Right":
                Nudge(new Vec2(shift ? 1f : 10f, 0f));
                return true;
            case "ArrowUp":
            case "Up":
                Nudge(new Vec2(0f, shift ? 1f : 10f));
                return true;
            case "ArrowDown":
            case "Down":
                Nudge(new Vec2(0f, shift ? -1f : -10f));
                return true;
            case "BracketLeft":
            case "[":
                StepGlyph(-1);
                return true;
            case "BracketRight":
            case "]":
                StepGlyph(1);
                return true;
        }

        return false;
    }

    /// <summary>Nudges the selected nodes or the whole glyph.</summary>
    public void Nudge(Vec2 delta)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        PushUndo();
        if (SelectedContour >= 0 && SelectedContour < glyph.Contours.Count && SelectedNodes.Count > 0)
        {
            GlyphContour contour = glyph.Contours[SelectedContour];
            foreach (int index in SelectedNodes)
            {
                if (index >= 0 && index < contour.Nodes.Count)
                {
                    contour.Nodes[index].Translate(delta);
                }
            }
        }
        else
        {
            glyph.Translate(delta);
        }

        _geometryVersion++;
        Project.Dirty = true;
        UpdateStatus();
        Notify();
    }
}
