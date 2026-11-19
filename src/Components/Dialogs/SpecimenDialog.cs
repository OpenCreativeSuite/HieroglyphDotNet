using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>A large specimen sheet that shows the typeface the way a designer would present it.</summary>
internal sealed class SpecimenDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();

        Node canvas = Canvas(new CanvasProps
        {
            ClassName = "specimen-canvas",
            Attributes = Ui.Size(900, 540),
            OnDraw = context => Draw(context, session, canvasRef.Value),
        });

        if (canvas is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return Div(new() { ClassName = "modal specimen-dialog is-bare", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" },
                    Text($"{session.Project.FamilyName} {session.Style.Name} — specimen")),
                Span(new() { ClassName = "modal-head-spacer" }),
                Button(new() { ClassName = "btn-tiny", OnClick = session.OpenExport },
                    Ui.Icon(Icons.Export), Text("Export")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body specimen-body" },
                Div(new() { ClassName = "specimen-toolbar" },
                    Span(new() { ClassName = "field-label" }, Text("Sample text")),
                    Input(new()
                    {
                        ClassName = "text-field-wide",
                        Type = "text",
                        Value = session.PreviewText,
                        Placeholder = "Type anything",
                        OnInput = value =>
                        {
                            session.PreviewText = value;
                            session.Notify();
                        },
                        OnChange = value => session.PreviewText = value,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    })),
                Div(new() { ClassName = "specimen-canvas-wrap" }, canvas)));
    }

    private static void Draw(CanvasContext context, FontEditor session, ElementNode? canvas)
    {
        if (canvas is null || canvas.Width < 40f)
        {
            return;
        }

        float width = canvas.Width;
        float height = canvas.Height;
        float left = 46f;
        uint ink = 0xFF14161Au;
        uint soft = 0xFF4A5058;
        FontStyle style = session.Style;

        context.FillRect(0f, 0f, width, height, 0xFFF4F3EF);

        context.DrawText(
            new Rect(left, 24f, width - (left * 2f), 14f),
            $"{session.Project.FamilyName.ToUpperInvariant()}  ·  {style.Name.ToUpperInvariant()}  ·  {style.Weight}",
            new TextStyle("Segoe UI", 10f, 700, false, 0xFF8A8578, 1.4f, 12f, TextAlign.Left, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));

        if (session.Project.DrawnCount == 0)
        {
            context.DrawText(
                new Rect(left, height * 0.5f, width - (left * 2f), 20f),
                "Draw a few characters to build a specimen.",
                new TextStyle("Segoe UI", 16f, 400, false, 0xFF8B94A1, 0f, 20f, TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
            return;
        }

        Typeface.DrawLine(context, session.RasterCache, style, "Aa", left, 208f, 0.152f, 0f, ink);
        Typeface.DrawLine(context, session.RasterCache, style, "abcdefghijklmnopqrstuvwxyz", left + 250f, 168f, 0.036f, 0f, soft);
        Typeface.DrawLine(context, session.RasterCache, style, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", left + 250f, 205f, 0.036f, 0f, soft);
        Typeface.DrawLine(context, session.RasterCache, style, "0123456789 .,:;!?()&@", left + 250f, 238f, 0.028f, 0f, soft);

        context.FillRect(left, 258f, width - (left * 2f), 1f, 0xFFD8D4C8);

        string sample = session.PreviewText.Length == 0 ? session.Project.SampleText : session.PreviewText;
        float baseline = 306f;
        List<string> sampleLines = Typeface.Wrap(style, sample, 0.042f, 0f, width - (left * 2f));
        for (int i = 0; i < sampleLines.Count && i < 2; i++)
        {
            Typeface.DrawLine(context, session.RasterCache, style, sampleLines[i], left, baseline, 0.042f, 0f, ink);
            baseline += 50f;
        }

        baseline += 12f;
        List<string> paragraph = Typeface.Wrap(
            style,
            "Sphinx of black quartz, judge my vow. Pack my box with five dozen liquor jugs. How vexingly quick daft zebras jump.",
            0.0175f,
            0f,
            width - (left * 2f));
        for (int i = 0; i < paragraph.Count && i < 3; i++)
        {
            Typeface.DrawLine(context, session.RasterCache, style, paragraph[i], left, baseline, 0.0175f, 0f, 0xFF34383Fu);
            baseline += 24f;
        }

        context.FillRect(left, height - 96f, width - (left * 2f), 1f, 0xFFD8D4C8);

        Typeface.DrawLine(context, session.RasterCache, style, "The quick brown fox jumps over the lazy dog", left, height - 62f, 0.02f, 0f, 0xFF2C3037);
        Typeface.DrawLine(context, session.RasterCache, style, "Hamburgefonstiv 2026 · AV WA To Ta Yo", left, height - 38f, 0.02f, 0f, 0xFF2C3037);

        context.DrawText(
            new Rect(left, height - 22f, width - (left * 2f), 12f),
            $"UNITS PER EM {style.Metrics.UnitsPerEm}   ·   CAP HEIGHT {style.Metrics.CapHeight}   ·   X-HEIGHT {style.Metrics.XHeight}   ·   {session.Project.DrawnCount} CHARACTERS",
            new TextStyle("Segoe UI", 8.5f, 600, false, 0xFF8A8578, 1.2f, 10f, TextAlign.Left, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
    }
}
