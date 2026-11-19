using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;

namespace HieroglyphDotNet.Components;

/// <summary>
/// The glyph drawing surface: one canvas paints the em panel, guides, the
/// outline, its points and every piece of editing chrome each frame.
/// </summary>
internal sealed class GlyphCanvas : Component
{
    private const uint Backdrop = 0xFF0E1013;
    private const uint Panel = 0xFF15181D;
    private const uint PanelEdge = 0xFF262C34;
    private const uint Ink = 0xFFE9EDF3;
    private const uint Accent = 0xFFFF8A3D;
    private const uint AccentSoft = 0x1FFF8A3D;
    private const uint GridMinor = 0x0DFFFFFF;
    private const uint GridMajor = 0x17FFFFFF;
    private const uint GuideStrong = 0x8FA8B3C2;
    private const uint GuideSoft = 0x3FA8B3C2;

    private static readonly TextStyle LabelStyle = new(
        "Segoe UI",
        9.5f,
        500,
        false,
        0xFF6E7885,
        0f,
        12f,
        TextAlign.Left,
        TextDecoration.None,
        WhiteSpace.NoWrap,
        Direction.LeftToRight);

    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();

        string cursor = session.Tool switch
        {
            GlyphTool.Pen => "is-pen",
            GlyphTool.Hand => "is-hand",
            GlyphTool.Zoom => "is-zoom",
            GlyphTool.Node => "is-node",
            _ => "is-select",
        };

        Node node = Canvas(new CanvasProps
        {
            ClassName = "surface " + cursor,
            TabIndex = 0,
            Attributes = Ui.Size(1100, 760),
            OnDraw = context => Draw(context, session, canvasRef.Value),
            OnPointerDown = (x, y) => session.PointerDown(
                new Vec2(x, y),
                InputState.Shift,
                InputState.Alt,
                InputState.Middle),
            OnPointerMove = (x, y) =>
            {
                if (CanvasPan.Handle(session))
                {
                    return;
                }

                session.PointerMove(new Vec2(x, y), InputState.Shift);
            },
            OnPointerUp = (x, y) =>
            {
                CanvasPan.Reset();
                session.PointerUp(new Vec2(x, y));
            },
            OnScrollEvent = scroll =>
            {
                Vec2 screen = canvasRef.Value is { } canvas
                    ? new Vec2(InputState.PointerX - canvas.PositionX, InputState.PointerY - canvas.PositionY)
                    : new Vec2(session.ViewWidth * 0.5f, session.ViewHeight * 0.5f);
                bool control = scroll.Modifiers.HasFlag(KeyModifiers.Control) || InputState.Control;
                bool shift = scroll.Modifiers.HasFlag(KeyModifiers.Shift) || InputState.Shift;
                session.Scroll(scroll.DeltaX, scroll.DeltaY, screen, control, shift);
            },
            OnContextMenu = menu =>
            {
                menu.PreventDefault();
                session.ClearSelection();
                session.Notify();
            },
        });

        if (node is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return node;
    }

    private static void Draw(CanvasContext context, FontEditor session, ElementNode? canvas)
    {
        if (canvas is null || canvas.Width < 4f || canvas.Height < 4f)
        {
            return;
        }

        session.SetViewSize(canvas.Width, canvas.Height);
        context.Clear(Backdrop);

        GlyphView view = session.View;
        FontMetrics metrics = session.Style.Metrics;
        Glyph? glyph = session.SelectedGlyph;
        float advance = glyph?.AdvanceWidth ?? 600;
        float zoom = view.Zoom;

        // ---------------------------------------------------------- em panel
        Vec2 topLeft = view.ToScreen(-70f, metrics.Ascender + 70f);
        Vec2 bottomRight = view.ToScreen(advance + 70f, metrics.Descender - 70f);
        Rect panel = new(
            topLeft.X,
            topLeft.Y,
            MathF.Max(2f, bottomRight.X - topLeft.X),
            MathF.Max(2f, bottomRight.Y - topLeft.Y));

        context.FillRoundedRect(panel, Panel, new CornerRadius(5, 5, 5, 5));
        context.PushClip(panel);

        // ------------------------------------------------------------- grid
        if (session.ShowGrid)
        {
            float step = 50f;
            while (step * zoom < 7f)
            {
                step *= 2f;
            }

            int index = 0;
            for (float x = 0f; x <= advance + 0.5f; x += step)
            {
                Vec2 a = view.ToScreen(x, metrics.Descender - 70f);
                context.FillRect(a.X, panel.Y, 1f, panel.Height, index % 2 == 0 ? GridMajor : GridMinor);
                index++;
            }

            index = 0;
            for (float y = metrics.Descender - 70f; y <= metrics.Ascender + 70f; y += step)
            {
                Vec2 a = view.ToScreen(0f, y);
                if (a.Y >= panel.Y && a.Y <= panel.Bottom)
                {
                    context.FillRect(panel.X, a.Y, panel.Width, 1f, index % 2 == 0 ? GridMajor : GridMinor);
                }

                index++;
            }
        }

        // Sidebearing shading: the parts of the advance that hold no ink.
        if (session.ShowBearings && glyph?.HasOutline == true)
        {
            float lsb = MathF.Max(0f, glyph.LeftSideBearing);
            float rsbStart = MathF.Min(advance, glyph.RightEdge);
            Vec2 leftEdge = view.ToScreen(0f, 0f);
            Vec2 leftInk = view.ToScreen(lsb, 0f);
            if (leftInk.X > leftEdge.X + 1f)
            {
                context.FillRect(leftEdge.X, panel.Y, leftInk.X - leftEdge.X, panel.Height, AccentSoft);
            }

            Vec2 rightInk = view.ToScreen(rsbStart, 0f);
            Vec2 rightEdge = view.ToScreen(advance, 0f);
            if (rightEdge.X > rightInk.X + 1f)
            {
                context.FillRect(rightInk.X, panel.Y, rightEdge.X - rightInk.X, panel.Height, AccentSoft);
            }
        }

        // --------------------------------------------------------- the glyph
        if (glyph is { HasOutline: true })
        {
            Vec2 origin = view.ToScreen(0f, 0f);
            Typeface.DrawGlyph(context, session.RasterCache, glyph, zoom, origin.X, origin.Y, Ink);
        }

        // The live brush stroke is painted exactly as it will be committed.
        if (session.BrushPreview is { } preview)
        {
            Vec2 origin = view.ToScreen(0f, 0f);
            GlyphBitmap bitmap = GlyphRasterizer.Rasterize(preview, zoom);
            GlyphRasterizer.Paint(
                context,
                bitmap,
                origin.X - bitmap.OriginX,
                origin.Y - bitmap.OriginY,
                0xE6E9EDF3u);
        }

        // -------------------------------------------------------- metric lines
        if (session.ShowMetrics)
        {
            DrawGuide(context, view, panel, metrics.Descender, GuideSoft, session, "Descender", true);
            DrawGuide(context, view, panel, metrics.Ascender, GuideSoft, session, "Ascender", true);
            DrawGuide(context, view, panel, metrics.CapHeight, GuideSoft, session, "Cap height", true);
            DrawGuide(context, view, panel, metrics.XHeight, GuideSoft, session, "x-height", true);
            DrawGuide(context, view, panel, 0f, GuideStrong, session, "Baseline", false);
        }
        else
        {
            DrawGuide(context, view, panel, 0f, GuideStrong, session, "", false);
        }

        if (session.ShowBearings)
        {
            DrawVertical(context, view, panel, 0f, 0x99FF8A3D);
            DrawVertical(context, view, panel, advance, 0x99FF8A3D);
        }

        // --------------------------------------------------------- selection
        DrawContours(context, session, view);
        DrawMarquee(context, session);
        DrawPenPreview(context, session, view);

        if (session.Tool == GlyphTool.Brush && session.CursorOnCanvas)
        {
            Vec2 centre = view.ToScreen(session.CursorUnits);
            Circle(context, centre, session.BrushWidth * 0.5f * zoom, 0xCCFF8A3D);
        }

        context.PopClip();

        // Panel edge last so guides do not bleed over it.
        Outline(context, panel, PanelEdge);

        // ----------------------------------------------------- advance readout
        if (session.ShowBearings)
        {
            Vec2 handle = view.ToScreen(advance, 0f);
            float chipY = handle.Y - 13f;
            context.FillRoundedRect(new Rect(handle.X - 24f, chipY, 48f, 17f), 0xFF23282F, new CornerRadius(3, 3, 3, 3));
            Outline(context, new Rect(handle.X - 24f, chipY, 48f, 17f), 0x66FF8A3D);
            context.DrawText(
                new Rect(handle.X - 24f, chipY + 2f, 48f, 14f),
                advance.ToString("0"),
                new TextStyle("Segoe UI", 10f, 600, false, 0xFFFFB07A, 0f, 12f, TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
            context.FillRect(handle.X, handle.Y - 4f, 1f, 8f, Accent);
        }
    }

    private static void DrawGuide(
        CanvasContext context,
        GlyphView view,
        Rect panel,
        float units,
        uint colour,
        FontEditor session,
        string label,
        bool dashed)
    {
        Vec2 point = view.ToScreen(0f, units);
        if (point.Y < panel.Y - 1f || point.Y > panel.Bottom + 1f)
        {
            return;
        }

        if (dashed)
        {
            for (float x = panel.X; x < panel.Right; x += 7f)
            {
                context.FillRect(x, point.Y, 4f, 1f, colour);
            }
        }
        else
        {
            context.FillRect(panel.X, point.Y, panel.Width, 1f, colour);
        }

        if (label.Length > 0 && point.Y > 12f && point.Y < panel.Bottom - 4f)
        {
            context.DrawText(
                new Rect(panel.X + 6f, point.Y - 13f, 140f, 12f),
                label,
                LabelStyle);
            context.DrawText(
                new Rect(panel.X + 6f + 62f, point.Y - 13f, 60f, 12f),
                units.ToString("0"),
                LabelStyle);
        }
    }

    private static void DrawVertical(CanvasContext context, GlyphView view, Rect panel, float units, uint colour)
    {
        Vec2 point = view.ToScreen(units, 0f);
        if (point.X < panel.X || point.X > panel.Right)
        {
            return;
        }

        context.FillRect(point.X, panel.Y, 1f, panel.Height, colour);
    }

    private static void DrawContours(CanvasContext context, FontEditor session, GlyphView view)
    {
        Glyph? glyph = session.SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        bool editing = session.Tool is GlyphTool.Node or GlyphTool.Pen or GlyphTool.Select;
        float zoom = view.Zoom;

        for (int c = 0; c < glyph.Contours.Count; c++)
        {
            GlyphContour contour = glyph.Contours[c];
            if (!contour.IsDrawable)
            {
                continue;
            }

            bool selectedContour = c == session.SelectedContour;
            uint lineColour = selectedContour ? 0xAAFF8A3D : 0x38FFFFFF;

            List<Vec2> points = GlyphRasterizer.Flatten(
                contour,
                zoom,
                view.PanX,
                view.PanY);

            for (int i = 0; i + 1 < points.Count; i++)
            {
                Line(context, points[i], points[i + 1], lineColour);
            }
        }

        if (!editing)
        {
            return;
        }

        for (int c = 0; c < glyph.Contours.Count; c++)
        {
            GlyphContour contour = glyph.Contours[c];
            for (int n = 0; n < contour.Nodes.Count; n++)
            {
                GlyphNode node = contour.Nodes[n];
                Vec2 point = view.ToScreen(node.Point);
                bool selected = c == session.SelectedContour && session.SelectedNodes.Contains(n);

                if (selected)
                {
                    if (node.HasIn)
                    {
                        Vec2 handle = view.ToScreen(node.In);
                        Line(context, point, handle, 0xAAFF8A3D);
                        Handle(context, handle, false);
                    }

                    if (node.HasOut)
                    {
                        Vec2 handle = view.ToScreen(node.Out);
                        Line(context, point, handle, 0xAAFF8A3D);
                        Handle(context, handle, false);
                    }
                }
                else if (node.HasIn || node.HasOut)
                {
                    if (node.HasIn)
                    {
                        Vec2 handle = view.ToScreen(node.In);
                        Line(context, point, handle, 0x33FFFFFF);
                        Handle(context, handle, true);
                    }

                    if (node.HasOut)
                    {
                        Vec2 handle = view.ToScreen(node.Out);
                        Line(context, point, handle, 0x33FFFFFF);
                        Handle(context, handle, true);
                    }
                }

                float size = selected ? 9f : 6.5f;
                context.FillRect(point.X - (size * 0.5f) - 1f, point.Y - (size * 0.5f) - 1f, size + 2f, size + 2f, 0xFF0C0E11);
                context.FillRect(
                    point.X - (size * 0.5f),
                    point.Y - (size * 0.5f),
                    size,
                    size,
                    selected ? Accent : (node.Smooth ? 0xFFEFF6FF : 0xFFD3DAE4));
            }
        }
    }

    private static void Handle(CanvasContext context, Vec2 point, bool faint)
    {
        uint fill = faint ? 0x88C9D2DE : 0xFFEFF4FA;
        context.FillRect(point.X - 3.5f, point.Y - 3.5f, 7f, 7f, 0xFF0C0E11);
        context.FillRect(point.X - 2.5f, point.Y - 2.5f, 5f, 5f, fill);
    }

    private static void DrawPenPreview(CanvasContext context, FontEditor session, GlyphView view)
    {
        if (!session.IsPenActive || session.PenPreview is not { } last)
        {
            return;
        }

        Vec2 from = view.ToScreen(last);
        Vec2 to = view.ToScreen(session.CursorUnits);
        DashLine(context, from.X, from.Y, to.X, to.Y, 0xAAFF8A3D);

        Vec2 target = view.ToScreen(session.CursorUnits);
        context.FillRect(target.X - 3f, target.Y - 3f, 6f, 6f, 0xFF0C0E11);
        context.FillRect(target.X - 2f, target.Y - 2f, 4f, 4f, Accent);
    }

    private static void DrawMarquee(CanvasContext context, FontEditor session)
    {
        if (session.Marquee is not { } marquee)
        {
            return;
        }

        float x = MathF.Min(marquee.Start.X, marquee.End.X);
        float y = MathF.Min(marquee.Start.Y, marquee.End.Y);
        float width = MathF.Abs(marquee.End.X - marquee.Start.X);
        float height = MathF.Abs(marquee.End.Y - marquee.Start.Y);
        if (width < 1f || height < 1f)
        {
            return;
        }

        context.FillRect(x, y, width, height, 0x22FF8A3D);
        DashLine(context, x, y, x + width, y, Accent);
        DashLine(context, x + width, y, x + width, y + height, Accent);
        DashLine(context, x + width, y + height, x, y + height, Accent);
        DashLine(context, x, y + height, x, y, Accent);
    }

    private static void Outline(CanvasContext context, Rect rect, uint colour)
    {
        context.FillRect(rect.X, rect.Y, rect.Width, 1f, colour);
        context.FillRect(rect.X, rect.Y + rect.Height - 1f, rect.Width, 1f, colour);
        context.FillRect(rect.X, rect.Y, 1f, rect.Height, colour);
        context.FillRect(rect.X + rect.Width - 1f, rect.Y, 1f, rect.Height, colour);
    }

    private static void Line(CanvasContext context, Vec2 from, Vec2 to, uint colour)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        int steps = Math.Max(1, (int)MathF.Ceiling(length / 1.6f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            context.FillRect(from.X + (dx * t), from.Y + (dy * t), 1.15f, 1.15f, colour);
        }
    }

    /// <summary>Draws a one-pixel circle outline.</summary>
    private static void Circle(CanvasContext context, Vec2 centre, float radius, uint colour)
    {
        if (radius < 1f)
        {
            return;
        }

        int steps = Math.Clamp((int)(radius * 1.6f), 16, 64);
        Vec2 previous = new(centre.X + radius, centre.Y);
        for (int i = 1; i <= steps; i++)
        {
            float angle = MathF.Tau * i / steps;
            Vec2 current = new(centre.X + (radius * MathF.Cos(angle)), centre.Y + (radius * MathF.Sin(angle)));
            Line(context, previous, current, colour);
            previous = current;
        }
    }

    private static void DashLine(CanvasContext context, float x0, float y0, float x1, float y1, uint colour)    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.5f)
        {
            return;
        }

        float ux = dx / length;
        float uy = dy / length;
        int steps = (int)MathF.Ceiling(length / 7f);
        for (int i = 0; i < steps; i++)
        {
            float start = i * 7f;
            float size = MathF.Min(4f, length - start);
            if (size <= 0f)
            {
                break;
            }

            context.FillRect(
                x0 + (ux * start),
                y0 + (uy * start),
                ux == 0f ? 1.2f : size,
                uy == 0f ? 1.2f : size,
                colour);
        }
    }
}
