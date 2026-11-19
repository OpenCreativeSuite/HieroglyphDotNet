using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Bottom bar: tool state, cursor position, zoom and the status line.</summary>
internal sealed class StatusBar : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.SelectedGlyph;

        string position = session.CursorOnCanvas
            ? $"{session.CursorUnits.X:0}, {session.CursorUnits.Y:0}"
            : "—";

        string character = glyph is null
            ? "No character"
            : $"{glyph.Character}  {glyph.Name}";

        return Div(new() { ClassName = "statusbar" },
            Span(new() { ClassName = "status-item status-tool" }, Text(FontEditor.ToolName(session.Tool))),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-strong" }, Text(character)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item" }, Text(session.SelectionLabel)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text(position)),
            Div(new() { ClassName = "status-spacer" }),
            Span(new() { ClassName = "status-message" }, Text(session.Status)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text($"{session.Style.Metrics.UnitsPerEm} upm")),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text($"{session.Zoom * 100f:0}%")));
    }
}
