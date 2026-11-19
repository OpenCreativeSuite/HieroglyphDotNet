using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Second bar: file actions, undo, zoom, the active style and the preview toggle.</summary>
internal sealed class Toolbar : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);

        return Div(new() { ClassName = "toolbar" },
            Ui.OptionsGroup(
                Ui.ActionButton(Icons.Document, "New", "New font (Ctrl+N)", session.OpenNewFont),
                Ui.ActionButton(Icons.Folder, "Open", "Open a project (Ctrl+O)", session.OpenProject),
                Ui.ActionButton(Icons.Save, "Save", "Save the project (Ctrl+S)", session.SaveProject),
                Ui.ActionButton(Icons.Export, "Export", "Export a font file (Ctrl+E)", session.OpenExport, primary: true)),
            Ui.OptionsSeparator(),
            Ui.OptionsGroup(
                Ui.IconButton(Icons.Undo, "Undo (Ctrl+Z)", session.Undo, session.CanUndo),
                Ui.IconButton(Icons.Redo, "Redo (Ctrl+Y)", session.Redo, session.CanRedo)),
            Ui.OptionsSeparator(),
            ZoomGroup(session),
            Ui.OptionsSeparator(),
            Ui.OptionsGroup(
                Ui.IconButton(Icons.Grid, "Show the design grid", session.ToggleGrid, active: session.ShowGrid),
                Ui.IconButton(Icons.Ruler, "Show metric guides", session.ToggleMetrics, active: session.ShowMetrics),
                Ui.IconButton(Icons.Swap, "Show sidebearing guides", session.ToggleBearings, active: session.ShowBearings)),
            Div(new() { ClassName = "options-spacer" }),
            Button(new()
            {
                ClassName = "style-switcher",
                Title = "Manage styles and weights",
                OnClick = session.OpenStyles,
            },
            Span(new() { ClassName = "style-switcher-family" }, Text(session.Project.FamilyName)),
            Span(new() { ClassName = "style-switcher-style" }, Text(session.Style.Name)),
            Span(new() { ClassName = "style-switcher-caret" }, Text("▾"))),
            Ui.IconButton(
                Icons.TextHeight,
                "Show or hide the preview panel",
                session.TogglePreview,
                active: session.ShowPreview));
    }

    private static Node ZoomGroup(FontEditor session) =>
        Div(new() { ClassName = "options-zoom" },
            Ui.IconButton(Icons.Minus, "Zoom out (Ctrl+-)", () => session.ZoomStep(-1)),
            Button(new()
            {
                ClassName = "zoom-value",
                Title = "Fit the character to the canvas (Ctrl+0)",
                OnClick = () => session.FitGlyph(),
            }, Text($"{session.Zoom * 100f:0}%")),
            Ui.IconButton(Icons.Plus, "Zoom in (Ctrl+=)", () => session.ZoomStep(1)));
}
