using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Add, copy, rename and remove the styles in the family.</summary>
internal sealed class StylesDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        var (name, setName) = State("Bold");
        var (weight, setWeight) = State("700");
        var (italic, setItalic) = State(false);
        var (rename, setRename) = State(session.Style.Name);

        List<Node> rows = [];
        for (int i = 0; i < session.Project.Styles.Count; i++)
        {
            int index = i;
            FontStyle style = session.Project.Styles[i];
            bool active = index == session.Project.ActiveStyleIndex;
            rows.Add(Div(new()
            {
                ClassName = active ? "style-row is-active" : "style-row",
                OnClick = () => session.SetActiveStyle(index),
            },
            Div(new() { ClassName = "style-row-swatch" },
                Span(new() { ClassName = "style-row-live" },
                    Text(style.Name.Length > 0 ? style.Name[..1].ToUpperInvariant() : "A"))),
            Div(new() { ClassName = "style-row-text" },
                Div(new() { ClassName = "style-row-name" }, Text(style.Name)),
                Div(new() { ClassName = "style-row-meta" },
                    Text($"Weight {style.Weight}  ·  width {style.WidthClass}  ·  {style.DrawnCount} drawn{(style.Italic ? "  ·  italic" : "")}"))),
            Show(active, Span(new() { ClassName = "style-row-badge" }, Text("Editing")))));
        }

        return Div(new() { ClassName = "modal styles-dialog is-bare", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text("Styles and weights")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body" },
                Div(new() { ClassName = "field-label" }, Text("Styles in this family")),
                Div(new() { ClassName = "style-list" }, rows.ToArray()),
                Div(new() { ClassName = "panel-hint" },
                    Text("Each style has its own outlines and metrics. Activate a style to edit it, then shape the letters until the weight feels right.")),
                Div(new() { ClassName = "panel-divider" }),
                Div(new() { ClassName = "field-label" }, Text("Add a style copied from the current one")),
                Div(new() { ClassName = "style-add" },
                    Input(new()
                    {
                        ClassName = "text-field-wide",
                        Type = "text",
                        Value = name,
                        Placeholder = "Style name",
                        OnInput = setName,
                        OnChange = setName,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Input(new()
                    {
                        ClassName = "num-field style-weight",
                        Type = "text",
                        Value = weight,
                        Placeholder = "400",
                        OnInput = setWeight,
                        OnChange = setWeight,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Button(new()
                    {
                        ClassName = italic ? "btn-tiny is-on" : "btn-tiny",
                        Title = "Mark the new style as italic",
                        OnClick = () => setItalic(!italic),
                    }, Text("Italic")),
                    Button(new()
                    {
                        ClassName = "btn is-primary",
                        OnClick = () => session.AddStyle(name, Ui.ParseInt(weight, 700), italic),
                    }, Text("Add"))),
                Div(new() { ClassName = "panel-divider" }),
                Div(new() { ClassName = "field-label" }, Text("Rename the active style")),
                Div(new() { ClassName = "style-add" },
                    Input(new()
                    {
                        ClassName = "text-field-wide",
                        Type = "text",
                        Value = rename,
                        OnInput = setRename,
                        OnChange = value =>
                        {
                            setRename(value);
                            session.RenameStyle(value);
                        },
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    }),
                    Button(new()
                    {
                        ClassName = "btn",
                        OnClick = () => session.RenameStyle(rename),
                    }, Text("Rename")),
                    Button(new()
                    {
                        ClassName = "btn",
                        Disabled = session.Project.Styles.Count <= 1,
                        OnClick = session.DeleteStyle,
                    }, Ui.Icon(Icons.Trash), Text("Delete")))));
    }
}
