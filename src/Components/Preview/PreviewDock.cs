using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The collapsible bottom dock: live text, alphabet and kerning previews.</summary>
internal sealed class PreviewDock : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();

        Node canvas = Canvas(new CanvasProps
        {
            ClassName = "preview-canvas",
            Attributes = Ui.Size(1200, 180),
            OnDraw = context => Draw(context, session, canvasRef.Value),
        });

        if (canvas is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return Div(new() { ClassName = "preview" },
            Div(new() { ClassName = "preview-head" },
                Div(new() { ClassName = "preview-tabs" },
                    Tab(session, PreviewTab.Text, "Text"),
                    Tab(session, PreviewTab.Alphabet, "Alphabet"),
                    Tab(session, PreviewTab.Kerning, "Kerning")),
                Show(session.PreviewTab == PreviewTab.Text,
                    Input(new()
                    {
                        ClassName = "preview-text",
                        Type = "text",
                        Value = session.PreviewText,
                        Placeholder = "Type something to preview it",
                        OnInput = value =>
                        {
                            session.PreviewText = value;
                            session.Notify();
                        },
                        OnChange = value => session.PreviewText = value,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    })),
                Div(new() { ClassName = "preview-controls" },
                    CompactSlider(session, "Size", session.PreviewSize, 12f, 180f, $"{session.PreviewSize:0}", value =>
                    {
                        session.PreviewSize = Math.Clamp(Ui.Parse(value, session.PreviewSize), 8f, 320f);
                        session.Notify();
                    }),
                    CompactSlider(session, "Tracking", session.PreviewTracking, -80f, 200f, $"{session.PreviewTracking:0}", value =>
                    {
                        session.PreviewTracking = Ui.Parse(value, session.PreviewTracking);
                        session.Notify();
                    }),
                    CompactSlider(session, "Leading", session.PreviewLeading, 0.8f, 2.4f, $"{session.PreviewLeading:0.00}", value =>
                    {
                        session.PreviewLeading = Math.Clamp(Ui.Parse(value, session.PreviewLeading), 0.6f, 3f);
                        session.Notify();
                    }),
                    Ui.IconButton(
                        Icons.Contrast,
                        session.PreviewDark ? "Switch to dark ink on paper" : "Switch to light ink on charcoal",
                        () =>
                        {
                            session.PreviewDark = !session.PreviewDark;
                            session.Notify();
                        },
                        active: session.PreviewDark),
                    Ui.IconButton(Icons.ChevronDown, "Collapse the preview", session.TogglePreview))),
            Div(new() { ClassName = "preview-body" }, canvas));
    }

    private static Node Tab(FontEditor session, PreviewTab tab, string label) =>
        Button(new()
        {
            ClassName = session.PreviewTab == tab ? "preview-tab is-active" : "preview-tab",
            OnClick = () => session.SetPreviewTab(tab),
        }, Text(label));

    private static Node CompactSlider(
        FontEditor session,
        string label,
        float value,
        float min,
        float max,
        string display,
        Action<string> onInput) =>
        Div(new() { ClassName = "mini-slider", Title = label },
            Span(new() { ClassName = "mini-slider-label" }, Text(label)),
            Input(new()
            {
                ClassName = "mini-slider-input",
                Type = "range",
                Value = Ui.Number(value),
                Attributes = new()
                {
                    ["min"] = Ui.Number(min),
                    ["max"] = Ui.Number(max),
                    ["step"] = Ui.Number((max - min) / 200f),
                },
                OnInput = onInput,
                OnChange = onInput,
                OnFocus = Ui.Focus(session),
                OnBlur = Ui.Blur(session),
            }),
            Span(new() { ClassName = "mini-slider-value" }, Text(display)));

    private static void Draw(CanvasContext context, FontEditor session, ElementNode? canvas)
    {
        if (canvas is null || canvas.Width < 8f || canvas.Height < 8f)
        {
            return;
        }

        Rect box = new(0f, 0f, canvas.Width, canvas.Height);
        uint paper = session.PreviewDark ? 0xFF101216u : 0xFFF1F3F6u;
        uint ink = session.PreviewDark ? 0xFFEDF1F7u : 0xFF16181Du;

        context.FillRoundedRect(box, paper, new CornerRadius(8, 8, 8, 8));

        FontStyle style = session.Style;
        float upm = MathF.Max(16, style.Metrics.UnitsPerEm);
        const float padding = 26f;

        if (session.Project.DrawnCount == 0)
        {
            context.DrawText(
                new Rect(padding, 76f, box.Width - (padding * 2f), 20f),
                "Draw a character and it will appear here.",
                new TextStyle("Segoe UI", 14f, 400, false, 0xFF8B94A1, 0f, 18f, TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
            return;
        }

        if (session.PreviewTab == PreviewTab.Text)
        {
            float scale = MathF.Max(0.006f, session.PreviewSize / upm);
            float leading = session.PreviewSize * session.PreviewLeading;
            float baseline = 24f + (session.PreviewSize * 0.74f);
            string text = session.PreviewText.Length == 0 ? session.Project.SampleText : session.PreviewText;
            List<string> lines = Typeface.Wrap(style, text, scale, session.PreviewTracking, box.Width - (padding * 2f));

            for (int i = 0; i < lines.Count && baseline < box.Height + leading; i++)
            {
                Typeface.DrawLine(context, session.RasterCache, style, lines[i], padding, baseline, scale, session.PreviewTracking, ink);
                baseline += leading;
            }

            return;
        }

        if (session.PreviewTab == PreviewTab.Alphabet)
        {
            DrawRow(context, session, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", padding, 46f, 34f, ink);
            DrawRow(context, session, "abcdefghijklmnopqrstuvwxyz", padding, 86f, 34f, ink);
            DrawRow(context, session, "0123456789  .,:;!?'\"-()/&", padding, 122f, 24f, ink);
            DrawRow(context, session, "AV WA To Ta Yo LT", padding, 158f, 22f, ink);
            return;
        }

        DrawRow(context, session, "AV WA To Ta Yo LT P. F,", padding, 62f, 54f, ink);
        DrawRow(context, session, "Handgloves 2026", padding, 130f, 40f, ink);
        context.DrawText(
            new Rect(padding, 12f, 400f, 14f),
            session.Style.Kerning.Count == 0 ? "No kerning pairs yet — add some in the inspector" : $"{session.Style.Kerning.Count} kerning pairs",
            new TextStyle("Segoe UI", 10f, 600, false, 0xFF8B94A1, 0f, 12f, TextAlign.Left, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
    }

    private static void DrawRow(CanvasContext context, FontEditor session, string text, float x, float baseline, float pixels, uint colour)
    {
        float scale = pixels / MathF.Max(16, session.Style.Metrics.UnitsPerEm);
        Typeface.DrawLine(context, session.RasterCache, session.Style, text, x, baseline, scale, 0f, colour);
    }
}
