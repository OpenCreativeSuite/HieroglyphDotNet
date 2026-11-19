using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The first-launch screen: what this is, and three ways to begin.</summary>
internal sealed class WelcomeDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();

        Node specimen = Canvas(new CanvasProps
        {
            ClassName = "welcome-specimen",
            Attributes = Ui.Size(560, 330),
            OnDraw = context => DrawSpecimen(context, session, canvasRef.Value),
        });

        if (specimen is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return Div(new() { ClassName = "welcome-card", OnClick = () => { } },
            Div(new() { ClassName = "welcome-main" },
                Div(new() { ClassName = "welcome-brand" },
                    Div(new() { ClassName = "brand-mark" },
                        Span(new() { ClassName = "brand-bar" }),
                        Span(new() { ClassName = "brand-dot" })),
                    Span(new() { ClassName = "welcome-brand-name" },
                        Strong(new() { }, Text("Hieroglyph")), Text(".NET")),
                    Span(new() { ClassName = "welcome-brand-tag" }, Text("OPEN CREATIVE SUITE"))),
                H1(new() { ClassName = "welcome-title" }, Text("Make a typeface.")),
                P(new() { ClassName = "welcome-lede" },
                    Text("Draw a shape for every letter, watch the alphabet come together, then export a font you can install and use anywhere.")),
                Div(new() { ClassName = "welcome-actions" },
                    Button(new()
                    {
                        ClassName = "welcome-btn is-primary",
                        OnClick = session.OpenNewFont,
                    }, Ui.Icon(Icons.Plus), Text("Create a new font")),
                    Button(new()
                    {
                        ClassName = "welcome-btn",
                        OnClick = () =>
                        {
                            session.LoadSample();
                            session.OpenSpecimen();
                        },
                    }, Ui.Icon(Icons.Type), Text("See the starter typeface")),
                    Button(new()
                    {
                        ClassName = "welcome-btn",
                        OnClick = session.OpenProject,
                    }, Ui.Icon(Icons.Folder), Text("Open a project")),
                    Button(new()
                    {
                        ClassName = "welcome-link",
                        OnClick = session.CloseWelcome,
                    }, Text("I'll look around first →"))),
                Div(new() { ClassName = "welcome-steps" },
                    Step("1", "Pick a character", "The browser on the left lists every letter, number and mark."),
                    Step("2", "Shape it", "Drag points, pull curves, or start from a simple shape."),
                    Step("3", "Export", "One click writes a real TTF, OTF or web font.")),
                Div(new() { ClassName = "welcome-foot" },
                    Ui.Icon(Icons.Info),
                    Text("Everything stays on your machine. No account, no cloud, no subscription."))),
            Div(new() { ClassName = "welcome-side" },
                specimen,
                Div(new() { ClassName = "welcome-caption" },
                    Span(new() { ClassName = "welcome-caption-title" },
                        Text($"{session.Project.FamilyName} {session.Style.Name}")),
                    Span(new() { ClassName = "welcome-caption-note" },
                        Text($"Starter typeface · {session.Project.DrawnCount} characters drawn")))));
    }

    private static Node Step(string number, string title, string copy) =>
        Div(new() { ClassName = "welcome-step" },
            Span(new() { ClassName = "welcome-step-number" }, Text(number)),
            Div(new() { ClassName = "welcome-step-text" },
                Span(new() { ClassName = "welcome-step-title" }, Text(title)),
                Span(new() { ClassName = "welcome-step-copy" }, Text(copy))));

    private static void DrawSpecimen(CanvasContext context, FontEditor session, ElementNode? canvas)
    {
        if (canvas is null || canvas.Width < 10f)
        {
            return;
        }

        float width = canvas.Width;
        float height = canvas.Height;
        context.PushTransform(System.Numerics.Matrix3x2.CreateScale(width / 560f, height / 330f));
        context.FillRoundedRect(new Rect(0f, 0f, 560f, 330f), 0xFFF3F2EE, new CornerRadius(10, 10, 10, 10));

        FontStyle style = session.Style;
        uint ink = 0xFF15171Bu;

        if (session.Project.DrawnCount == 0)
        {
            context.DrawText(
                new Rect(0f, 156f, 560f, 20f),
                "Your alphabet will appear here.",
                new TextStyle("Segoe UI", 14f, 400, false, 0xFF8B94A1, 0f, 18f, TextAlign.Center, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
            context.PopTransform();
            return;
        }

        Typeface.DrawLine(context, session.RasterCache, style, "Aa", 30f, 128f, 0.122f, 0f, ink);
        Typeface.DrawLine(context, session.RasterCache, style, "Handgloves", 196f, 82f, 0.05f, 0f, ink);
        Typeface.DrawLine(context, session.RasterCache, style, "Sphinx of black quartz", 196f, 120f, 0.028f, 0f, 0xFF3A3F47);
        Typeface.DrawLine(context, session.RasterCache, style, "judge my vow · 0123456789", 196f, 148f, 0.0225f, 0f, 0xFF5A6069);
        Typeface.DrawLine(context, session.RasterCache, style, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 30f, 208f, 0.0215f, 0f, 0xFF2A2E35);
        Typeface.DrawLine(context, session.RasterCache, style, "abcdefghijklmnopqrstuvwxyz", 30f, 240f, 0.0215f, 0f, 0xFF2A2E35);
        Typeface.DrawLine(context, session.RasterCache, style, "0123456789 .,:;!?()+-=/", 30f, 272f, 0.0185f, 0f, 0xFF4A5058);

        context.FillRect(30f, 292f, 500f, 1f, 0x22000000);
        context.DrawText(
            new Rect(30f, 300f, 500f, 14f),
            "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG",
            new TextStyle("Segoe UI", 9.5f, 600, false, 0xFF7A8089, 1.6f, 12f, TextAlign.Left, TextDecoration.None, WhiteSpace.NoWrap, Direction.LeftToRight));
        context.PopTransform();
    }
}
