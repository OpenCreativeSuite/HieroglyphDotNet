using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The right column: context-sensitive properties for the character and the font.</summary>
internal sealed class InspectorPanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.SelectedGlyph;
        string subject = glyph is null ? "Font" : glyph.Character.Length > 0 ? glyph.Character : "Space";

        return Div(new() { ClassName = "side" },
            Div(new() { ClassName = "side-head" },
                Span(new() { ClassName = "side-head-title" }, Text("Inspector")),
                Span(new() { ClassName = "side-head-name" }, Text(subject))),
            C<GlyphPanel>(),
            C<MetricsPanel>(),
            C<KerningPanel>(),
            C<FontPanel>());
    }
}

/// <summary>Advance width, sidebearings, point details and quick shapes.</summary>
internal sealed class GlyphPanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.SelectedGlyph;
        bool drawn = glyph?.HasOutline == true;

        List<Node> rows =
        [
            Div(new() { ClassName = "glyph-card" },
                Canvas(new CanvasProps
                {
                    ClassName = "glyph-card-thumb",
                    Attributes = Ui.Size(58, 58),
                    OnDraw = context => DrawThumb(context, session),
                }),
                Div(new() { ClassName = "glyph-card-text" },
                    Div(new() { ClassName = "glyph-card-name" },
                        Text(glyph?.Name ?? "No character")),
                    Div(new() { ClassName = "glyph-card-code" },
                        Text($"U+{session.SelectedCode:X4}  ·  {CharacterSet.Label(CharacterSet.CategoryFor(session.SelectedCode))}")),
                    Div(new() { ClassName = "glyph-card-state" },
                        Span(new()
                        {
                            ClassName = drawn ? "state-dot is-drawn" : "state-dot is-blank",
                        }),
                        Text(drawn ? "Drawn" : "Still blank")))),
            Div(new() { ClassName = "field-grid" },
                Ui.NumField(session, "W", glyph?.AdvanceWidth ?? 0, value => session.SetAdvance(Ui.ParseInt(value, glyph?.AdvanceWidth ?? 0)), "Advance width"),
                Ui.NumField(session, "LSB", glyph?.LeftSideBearing ?? 0, value => session.SetLeftBearing(Ui.ParseInt(value, glyph?.LeftSideBearing ?? 0)), "Left sidebearing")),
            Div(new() { ClassName = "field-grid" },
                Ui.NumField(session, "RSB", glyph?.RightSideBearing ?? 0, value =>
                {
                    if (glyph is null)
                    {
                        return;
                    }

                    int rsb = Ui.ParseInt(value, glyph.RightSideBearing);
                    session.SetAdvance((int)MathF.Round(glyph.RightEdge) + rsb);
                }, "Right sidebearing"),
                Ui.Readout("Ink width", drawn ? Ui.Whole(glyph!.OutlineBounds.Width) : "—")),
            Div(new() { ClassName = "panel-hint" },
                Text("Advance is how far the next letter starts. Sidebearings are the air either side of the ink — drag the orange line on the canvas to change them.")),
            Div(new() { ClassName = "panel-actions" },
                Ui.MiniButton(Icons.Swap, "Flip horizontally", session.FlipHorizontal, enabled: drawn),
                Ui.MiniButton(Icons.Contrast, "Flip vertically", session.FlipVertical, enabled: drawn),
                Ui.MiniButton(Icons.AlignCenter, "Centre between sidebearings", session.CentreGlyph, enabled: drawn),
                Ui.MiniButton(Icons.Duplicate, "Duplicate to another character", session.OpenDuplicateDialog, enabled: drawn),
                Ui.MiniButton(Icons.Trash, "Clear the character", session.ClearGlyph, enabled: drawn))
        ];

        if (session.SelectedNode >= 0 && glyph is not null && session.SelectedContour >= 0 && session.SelectedContour < glyph.Contours.Count)
        {
            GlyphContour contour = glyph.Contours[session.SelectedContour];
            if (session.SelectedNode < contour.Nodes.Count)
            {
                GlyphNode node = contour.Nodes[session.SelectedNode];
                rows.Add(Div(new() { ClassName = "panel-divider" }));
                rows.Add(Div(new() { ClassName = "field-label" }, Text("Selected point")));
                rows.Add(Div(new() { ClassName = "field-grid" },
                    Ui.NumField(session, "X", node.Point.X, value => MoveNode(session, MathF.Round(Ui.Parse(value, node.Point.X)), node.Point.Y)),
                    Ui.NumField(session, "Y", node.Point.Y, value => MoveNode(session, node.Point.X, MathF.Round(Ui.Parse(value, node.Point.Y))))));
                rows.Add(Div(new() { ClassName = "choice-row" },
                    Button(new()
                    {
                        ClassName = node.Smooth ? "choice is-active" : "choice",
                        OnClick = session.ToggleNodeSmooth,
                    }, Text("Smooth")),
                    Button(new()
                    {
                        ClassName = node.Smooth ? "choice" : "choice is-active",
                        OnClick = session.ToggleNodeSmooth,
                    }, Text("Corner")),
                    Ui.MiniButton(Icons.Close, "Retract the outgoing handle", () => session.RetractHandle(true))));
            }
        }

        return C<Panel, PanelProps>(new PanelProps
        {
            Title = "Character",
            Badge = drawn ? "Drawn" : "Blank",
            Children = [.. rows],
        });
    }

    private static void MoveNode(FontEditor session, float x, float y)
    {
        Glyph? glyph = session.SelectedGlyph;
        if (glyph is null || session.SelectedContour < 0 || session.SelectedContour >= glyph.Contours.Count)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[session.SelectedContour];
        int index = session.SelectedNode;
        if (index < 0 || index >= contour.Nodes.Count)
        {
            return;
        }

        session.PushUndo();
        GlyphNode node = contour.Nodes[index];
        node.Translate(new Vec2(x - node.Point.X, y - node.Point.Y));
        session.Touch();
    }

    private static void DrawThumb(CanvasContext context, FontEditor session)
    {
        Glyph? glyph = session.SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        Rect box = new(0f, 0f, 58f, 58f);
        if (glyph.HasOutline)
        {
            Typeface.DrawInBox(context, session.RasterCache, session.Style, glyph, box, 0xFFEDF2F8, 0.6f);
            return;
        }

        TextStyle style = new(
            "Segoe UI", 24f, 400, false, 0xFF5D6875, 0f, 28f,
            TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight);
        context.DrawText(new Rect(0f, 14f, 58f, 30f), glyph.Character.Length > 0 ? glyph.Character : "␣", style);
    }
}
