using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>A visual kerning editor: adjust the air between two characters.</summary>
internal sealed class KerningPanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        var (leftText, setLeftText) = State("T");
        var (rightText, setRightText) = State("o");
        var (valueText, setValueText) = State("-60");

        List<(int Left, int Right, int Value)> pairs = session.KerningPairs();

        return C<Panel, PanelProps>(new PanelProps
        {
            Title = "Kerning",
            Badge = pairs.Count == 0 ? "None" : $"{pairs.Count} pairs",
            StartOpen = false,
            Children =
            [
                Div(new() { ClassName = "kern-preview" },
                    Canvas(new CanvasProps
                    {
                        ClassName = "kern-canvas",
                        Attributes = Ui.Size(264, 96),
                        OnDraw = context => DrawPair(context, session),
                    }),
                    Div(new() { ClassName = "kern-readout" },
                        Span(new() { ClassName = "kern-pair-label" },
                            Text($"{FontEditor.CharOf(session.KerningLeft)}{FontEditor.CharOf(session.KerningRight)}")),
                        Span(new() { ClassName = "kern-pair-value" }, Text($"{session.CurrentKern:0} units")))),
                Div(new() { ClassName = "kern-controls" },
                    Ui.MiniButton(Icons.Minus, "Loosen by 5 units", () => session.NudgeKern(-5)),
                    Input(new()
                    {
                        ClassName = "num-field kern-field",
                        Type = "text",
                        Value = session.CurrentKern.ToString("0"),
                        OnChange = value => session.SetKern(session.KerningLeft, session.KerningRight, Ui.ParseInt(value, session.CurrentKern)),
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Ui.MiniButton(Icons.Plus, "Tighten by 5 units", () => session.NudgeKern(5)),
                    Ui.MiniButton(Icons.Close, "Remove this pair", session.RemoveKern)),
                Div(new() { ClassName = "panel-hint" },
                    Text("Negative values pull the pair closer. Select a suggested pair to preview it, then nudge until the spacing looks even.")),
                Div(new() { ClassName = "kern-suggestions" },
                    Each(FontEditor.CommonPairs.Take(8), pair => (Node)Button(new()
                    {
                        ClassName = session.KerningLeft == pair.Left && session.KerningRight == pair.Right
                            ? "kern-chip is-active"
                            : "kern-chip",
                        Title = $"Kern {FontEditor.CharOf(pair.Left)}{FontEditor.CharOf(pair.Right)}",
                        OnClick = () => session.SelectKernPair(pair.Left, pair.Right),
                    }, Text($"{FontEditor.CharOf(pair.Left)}{FontEditor.CharOf(pair.Right)}")))),
                Show(pairs.Count > 0,
                    Div(new() { ClassName = "kern-list" },
                        Each(pairs, pair => (Node)KernRow(session, pair)))),
                Div(new() { ClassName = "field-label" }, Text("Add a pair")),
                Div(new() { ClassName = "kern-add" },
                    Input(new()
                    {
                        ClassName = "num-field kern-char",
                        Type = "text",
                        Value = leftText,
                        Placeholder = "A",
                        OnChange = setLeftText,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Input(new()
                    {
                        ClassName = "num-field kern-char",
                        Type = "text",
                        Value = rightText,
                        Placeholder = "V",
                        OnChange = setRightText,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Input(new()
                    {
                        ClassName = "num-field kern-field",
                        Type = "text",
                        Value = valueText,
                        OnChange = setValueText,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Button(new()
                    {
                        ClassName = "btn-tiny",
                        OnClick = () =>
                        {
                            int left = FirstCodepoint(leftText);
                            int right = FirstCodepoint(rightText);
                            if (left > 0 && right > 0)
                            {
                                session.SetKern(left, right, Ui.ParseInt(valueText, -40));
                            }
                        },
                    }, Text("Add"))),
            ],
        });
    }

    private static Node KernRow(FontEditor session, (int Left, int Right, int Value) pair) =>
        Div(new()
        {
            ClassName = session.KerningLeft == pair.Left && session.KerningRight == pair.Right
                ? "kern-row is-active"
                : "kern-row",
            OnClick = () => session.SelectKernPair(pair.Left, pair.Right),
        },
        Span(new() { ClassName = "kern-row-pair" },
            Text($"{FontEditor.CharOf(pair.Left)}{FontEditor.CharOf(pair.Right)}")),
        Span(new() { ClassName = "kern-row-value" }, Text(pair.Value.ToString("0"))),
        Button(new()
        {
            ClassName = "kern-row-remove",
            Title = "Remove pair",
            OnClick = () => session.SetKern(pair.Left, pair.Right, 0),
        }, Ui.Icon(Icons.Close)));

    private static void DrawPair(CanvasContext context, FontEditor session)
    {
        Glyph? left = session.Style.FindGlyph(session.KerningLeft);
        Glyph? right = session.Style.FindGlyph(session.KerningRight);
        if (left is null || right is null)
        {
            return;
        }

        Rect box = new(0f, 0f, 264f, 96f);
        context.FillRoundedRect(box, 0xFF12151A, new CornerRadius(6, 6, 6, 6));

        float kern = session.CurrentKern;
        float totalUnits = MathF.Max(200f, left.AdvanceWidth + right.AdvanceWidth + kern);
        float scale = MathF.Min((box.Width - 46f) / totalUnits, 54f / 1000f);
        float baseline = 70f;
        float start = (box.Width - (totalUnits * scale)) * 0.5f;

        Typeface.DrawGlyph(context, session.RasterCache, left, scale, start, baseline, 0xFFECF1F8);
        float pen = start + ((left.AdvanceWidth + kern) * scale);
        Typeface.DrawGlyph(context, session.RasterCache, right, scale, pen, baseline, 0xFFECF1F8);

        // The gap between the two advances.
        float gap = start + (left.AdvanceWidth * scale);
        context.FillRect(gap, 16f, 1f, 64f, 0x33FF8A3D);
        context.FillRect(pen, 16f, 1f, 64f, 0x55FF8A3D);
        context.DrawText(
            new Rect(6f, 6f, 252f, 12f),
            $"{FontEditor.CharOf(session.KerningLeft)} {FontEditor.CharOf(session.KerningRight)}",
            new TextStyle("Segoe UI", 10f, 600, false, 0xFF8B94A1, 0f, 12f, TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
    }

    private static int FirstCodepoint(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        return char.ConvertToUtf32(trimmed, 0);
    }
}
