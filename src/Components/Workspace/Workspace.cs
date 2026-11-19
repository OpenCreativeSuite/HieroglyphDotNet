using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The centre column: the glyph bar, the tool rail and the drawing surface.</summary>
internal sealed class Workspace : Component
{
    private sealed record ToolEntry(GlyphTool Tool, string Icon, string Shortcut, bool StartsGroup = false);

    private static readonly ToolEntry[] Tools =
    [
        new(GlyphTool.Select, Icons.Select, "V"),
        new(GlyphTool.Node, Icons.Node, "A"),
        new(GlyphTool.Pen, Icons.Pen, "P", StartsGroup: true),
        new(GlyphTool.Brush, Icons.Brush, "B"),
        new(GlyphTool.Hand, Icons.Hand, "H", StartsGroup: true),
        new(GlyphTool.Zoom, Icons.Zoom, "Z"),
    ];

    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.SelectedGlyph;
        FontMetrics metrics = session.Style.Metrics;

        return Div(new() { ClassName = "stage" },
            Div(new() { ClassName = "glyphbar" },
                Canvas(new CanvasProps
                {
                    ClassName = "glyphbar-preview",
                    Attributes = Ui.Size(38, 38),
                    OnDraw = context => DrawPreview(context, session),
                }),
                Div(new() { ClassName = "glyphbar-titles" },
                    Div(new() { ClassName = "glyphbar-name" },
                        Span(new() { ClassName = "glyphbar-char" },
                            Text(glyph?.Character.Length > 0 ? glyph.Character : "?")),
                        Span(new() { ClassName = "glyphbar-label" },
                            Text(glyph is null ? "No character" : glyph.Name)),
                        Span(new() { ClassName = "glyphbar-code" },
                            Text($"U+{session.SelectedCode:X4}"))),
                    Div(new() { ClassName = "glyphbar-meta" },
                        Text(glyph?.HasOutline == true
                            ? $"{glyph.Contours.Count} contour{(glyph.Contours.Count == 1 ? "" : "s")}  ·  {glyph.NodeCount} points"
                            : "Nothing drawn yet"),
                        Text("  ·  "),
                        Text($"x-height {metrics.XHeight}  ·  cap {metrics.CapHeight}"))),
                Div(new() { ClassName = "options-spacer" }),
                Show(session.Tool == GlyphTool.Brush,
                    Div(new() { ClassName = "brush-bar" },
                        Span(new() { ClassName = "brush-bar-label" }, Text("Brush")),
                        Ui.MiniButton(Icons.Minus, "Thinner stroke", () => session.SetBrushWidth(session.BrushWidth - 10)),
                        Input(new()
                        {
                            ClassName = "num-field brush-bar-field",
                            Type = "text",
                            Value = Ui.Number(session.BrushWidth),
                            Title = "Brush width in font units",
                            OnChange = value => session.SetBrushWidth(Ui.Parse(value, session.BrushWidth)),
                            OnFocus = Ui.Focus(session),
                            OnBlur = Ui.Blur(session),
                        }),
                        Ui.MiniButton(Icons.Plus, "Thicker stroke", () => session.SetBrushWidth(session.BrushWidth + 10)),
                        Span(new() { ClassName = "brush-bar-unit" }, Text("units")))),
                Div(new() { ClassName = "glyphbar-stats" },
                    Stat("Advance", $"{glyph?.AdvanceWidth ?? 0}"),
                    Stat("LSB", $"{glyph?.LeftSideBearing ?? 0}"),
                    Stat("RSB", $"{glyph?.RightSideBearing ?? 0}"),
                    Stat("Width", $"{MathF.Round(glyph?.OutlineBounds.Width ?? 0f)}")),
                Div(new() { ClassName = "glyphbar-actions" },
                    Ui.MiniButton(Icons.Duplicate, "Duplicate this character to another one (Ctrl+D)", session.OpenDuplicateDialog, enabled: glyph?.HasOutline == true),
                    Ui.MiniButton(Icons.Swap, "Copy a template from another character", session.OpenDuplicateDialog),
                    Ui.MiniButton(Icons.Trash, "Clear this character", session.ClearGlyph, enabled: glyph?.HasOutline == true))),
            Div(new() { ClassName = "stage-body" },
                Div(new() { ClassName = "rail" },
                    Each(Tools, entry => (Node)ToolButton(session, entry)),
                    Div(new() { ClassName = "rail-sep" }),
                    Ui.IconButton(Icons.Grid, "Show the design grid", session.ToggleGrid, active: session.ShowGrid, className: "tool-btn"),
                    Ui.IconButton(Icons.Ruler, "Show metric guides", session.ToggleMetrics, active: session.ShowMetrics, className: "tool-btn"),
                    Ui.IconButton(Icons.Swap, "Show sidebearing guides", session.ToggleBearings, active: session.ShowBearings, className: "tool-btn"),
                    Div(new() { ClassName = "rail-spacer" }),
                    Ui.IconButton(Icons.Shapes, "Fit the character to the canvas (Ctrl+0)", () => session.FitGlyph(), className: "tool-btn")),
                Div(new() { ClassName = "canvas-wrap" },
                    C<GlyphCanvas>(),
                    Show(session.ShowStarterPanel && glyph?.HasOutline != true,
                        Div(new() { ClassName = "canvas-overlay" }, C<StarterCard>())),
                    Div(new() { ClassName = "canvas-legend" },
                        Span(new() { ClassName = "legend-item" },
                            Text(session.Tool switch
                            {
                                GlyphTool.Brush => "Drag to paint a stroke · B",
                                GlyphTool.Pen => "Click to place points, drag to curve · P",
                                _ => glyph?.HasOutline == true
                                    ? "Drag a point to shape it"
                                    : "The em square is 1000 units tall",
                            }))))));
    }

    private static Node Stat(string label, string value) =>
        Div(new() { ClassName = "stat" },
            Span(new() { ClassName = "stat-label" }, Text(label)),
            Span(new() { ClassName = "stat-value" }, Text(value)));

    private static Node ToolButton(FontEditor session, ToolEntry entry)
    {
        string title = entry.Shortcut.Length == 0
            ? FontEditor.ToolName(entry.Tool)
            : $"{FontEditor.ToolName(entry.Tool)} ({entry.Shortcut})";

        return F(
            Show(entry.StartsGroup, Div(new() { ClassName = "rail-sep" })),
            Button(new()
            {
                ClassName = session.Tool == entry.Tool ? "tool-btn is-active" : "tool-btn",
                Title = title,
                OnClick = () => session.SetTool(entry.Tool),
            },
            Ui.Icon(entry.Icon)));
    }

    private static void DrawPreview(CanvasContext context, FontEditor session)
    {
        Glyph? glyph = session.SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        Rect box = new(0f, 0f, 38f, 38f);
        if (glyph.HasOutline)
        {
            Typeface.DrawInBox(context, session.RasterCache, session.Style, glyph, box, 0xFFE6EBF2, 0.8f);
            return;
        }

        TextStyle style = new(
            "Segoe UI",
            18f,
            400,
            false,
            0xFF6B7480,
            0f,
            20f,
            TextAlign.Center,
            TextDecoration.None,
            WhiteSpace.NoWrap,
            Direction.LeftToRight);
        context.DrawText(new Rect(0f, 9f, 38f, 22f), glyph.Character.Length > 0 ? glyph.Character : "·", style);
    }
}

/// <summary>The inviting set of choices shown over a character that is still blank.</summary>
internal sealed class StarterCard : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.SelectedGlyph;

        return Div(new()
        {
            ClassName = "starter-card",
            OnClick = () => { },
        },
        Div(new() { ClassName = "starter-kicker" }, Text("BLANK CHARACTER")),
        Div(new() { ClassName = "starter-title" },
            Text(glyph is null ? "Pick a character" : $"{glyph.Character} is waiting")),
        Div(new() { ClassName = "starter-copy" },
            Text("Draw it with the pen, drop in a shape and reshape it, or copy a character you have already made.")),
        Div(new() { ClassName = "starter-actions" },
            Button(new()
            {
                ClassName = "starter-button is-primary",
                Title = "Paint the character with the freehand brush",
                OnClick = () => session.SetTool(GlyphTool.Brush),
            }, Ui.Icon(Icons.Brush), Text("Draw with Brush")),
            Button(new()
            {
                ClassName = "starter-button",
                Title = "Place points and pull curves",
                OnClick = () => session.SetTool(GlyphTool.Pen),
            }, Ui.Icon(Icons.Pen), Text("Pen")),
            Button(new() { ClassName = "starter-button", OnClick = session.AddStarterEllipse },
                Ui.Icon("fa-circle"), Text("Ellipse")),
            Button(new() { ClassName = "starter-button", OnClick = session.AddStarterRectangle },
                Ui.Icon("fa-square"), Text("Rectangle")),
            Button(new() { ClassName = "starter-button", OnClick = session.OpenDuplicateDialog },
                Ui.Icon(Icons.Duplicate), Text("Copy a character"))),
        Div(new() { ClassName = "starter-foot" },
            Ui.Icon(Icons.Info),
            Text("Drag freely — the brush turns your stroke into an editable outline.")));
    }
}
