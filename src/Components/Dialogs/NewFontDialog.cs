using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Names a new family and chooses how much of a head start to take.</summary>
internal sealed class NewFontDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        var (name, setName) = State("My Typeface");
        var (styleName, setStyleName) = State("Regular");
        var (template, setTemplate) = State("blank");

        return Div(new() { ClassName = "modal newfont-modal", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text("New font")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body" },
                Div(new() { ClassName = "field-stack" },
                    Div(new() { ClassName = "field-label" }, Text("Family name")),
                    Input(new()
                    {
                        ClassName = "text-field-wide is-large",
                        Type = "text",
                        Value = name,
                        Placeholder = "My Typeface",
                        OnInput = setName,
                        OnChange = setName,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Div(new() { ClassName = "panel-hint" },
                        Text("This is the name people will see in their font menu."))),
                Div(new() { ClassName = "field-stack" },
                    Div(new() { ClassName = "field-label" }, Text("First style")),
                    Input(new()
                    {
                        ClassName = "text-field-wide",
                        Type = "text",
                        Value = styleName,
                        Placeholder = "Regular",
                        OnInput = setStyleName,
                        OnChange = setStyleName,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    })),
                Div(new() { ClassName = "field-label" }, Text("Starting point")),
                Div(new() { ClassName = "template-grid" },
                    TemplateCard(
                        template == "blank",
                        "Blank canvas",
                        "Every character starts empty. Best when you want to draw each letter from scratch.",
                        () => setTemplate("blank")),
                    TemplateCard(
                        template == "sample",
                        "Starter outlines",
                        "Begin with a complete rounded sans and reshape it into your own. The fastest way to a working font.",
                        () => setTemplate("sample"))),
                Div(new() { ClassName = "panel-hint" },
                    Ui.Icon(Icons.Info),
                    Text("Nothing is permanent: the name, style and every outline can be changed at any time."))),
            Div(new() { ClassName = "modal-actions" },
                Button(new() { ClassName = "btn", OnClick = session.CloseDialog }, Text("Cancel")),
                Button(new()
                {
                    ClassName = "btn is-primary",
                    OnClick = () =>
                    {
                        if (template == "sample")
                        {
                            session.NewFromSample(name, styleName);
                        }
                        else
                        {
                            session.CreateBlank(name, styleName);
                        }
                    },
                }, Text("Create font"))));
    }

    private static Node TemplateCard(bool active, string title, string copy, Action onClick) =>
        Div(new()
        {
            ClassName = active ? "template-card is-active" : "template-card",
            OnClick = onClick,
        },
        Div(new() { ClassName = "template-title" }, Text(title)),
        Div(new() { ClassName = "template-copy" }, Text(copy)),
        Show(active, Span(new() { ClassName = "template-check" }, Ui.Icon(Icons.Check))));
}
