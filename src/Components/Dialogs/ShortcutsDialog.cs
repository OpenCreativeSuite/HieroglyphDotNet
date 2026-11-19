using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The full keyboard reference.</summary>
internal sealed class ShortcutsDialog : Component
{
    private static readonly (string Key, string Action)[] Tools =
    [
        ("V", "Selection — move whole characters"),
        ("A", "Node — edit points and handles"),
        ("P", "Pen — place anchors and pull curves"),
        ("B", "Brush — drag freely, get an editable outline"),
        ("H", "Hand — pan the canvas"),
        ("Z", "Zoom — click to zoom in, Alt-click out"),
        ("Ctrl+0", "Fit the character to the canvas"),
        ("Ctrl+= / Ctrl+-", "Zoom in and out"),
        ("Wheel", "Zoom around the pointer"),
        ("[ / ]", "Previous and next character"),
        ("Space-drag", "Pan with any tool"),
    ];

    private static readonly (string Key, string Action)[] Editing =
    [
        ("Ctrl+Z / Ctrl+Y", "Undo and redo"),
        ("Delete", "Delete the selected points"),
        ("Ctrl+A", "Select every point in the first contour"),
        ("Shift-click", "Add a point to the selection"),
        ("Shift-drag", "Constrain a move to one axis"),
        ("Arrows", "Nudge by 10 units (Shift: 1)"),
        ("Escape", "End a pen stroke, then deselect"),
        ("Enter", "Finish the current pen contour"),
        ("Ctrl+C / Ctrl+V", "Copy and paste a character"),
        ("Ctrl+D", "Copy a character to another one"),
        ("Ctrl+E", "Export a font"),
        ("Ctrl+S", "Save the project"),
    ];

    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);

        return Div(new() { ClassName = "modal shortcuts-dialog is-bare", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text("Keyboard shortcuts")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body shortcuts-body" },
                Div(new() { ClassName = "shortcuts-col" },
                    Div(new() { ClassName = "modal-group-title" }, Text("Tools and view")),
                    Each(Tools, row => (Node)Row(row.Key, row.Action))),
                Div(new() { ClassName = "shortcuts-col" },
                    Div(new() { ClassName = "modal-group-title" }, Text("Editing")),
                    Each(Editing, row => (Node)Row(row.Key, row.Action)))));
    }

    private static Node Row(string key, string action) =>
        Div(new() { ClassName = "shortcut-row" },
            Span(new() { }, Text(action)),
            Kbd(new() { }, Text(key)));
}
