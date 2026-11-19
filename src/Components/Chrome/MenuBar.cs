using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>One row in a drop-down menu.</summary>
internal sealed record MenuAction(
    string Label,
    string Shortcut,
    Action<FontEditor> Run,
    Func<FontEditor, bool>? Enabled = null,
    Func<FontEditor, bool>? Checked = null,
    bool StartsGroup = false);

/// <summary>A titled drop-down.</summary>
internal sealed record MenuDefinition(string Title, MenuAction[] Items);

/// <summary>Top bar with the application menus and the document readout.</summary>
internal sealed class MenuBar : Component
{
    internal static readonly MenuDefinition[] Menus = Build();

    /// <summary>Finds a menu by title.</summary>
    internal static MenuDefinition? Find(string? title) =>
        title is null ? null : Menus.FirstOrDefault(menu => menu.Title == title);

    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);

        return Div(new() { ClassName = "menubar" },
            Div(new() { ClassName = "brand" },
                Div(new() { ClassName = "brand-mark" },
                    Span(new() { ClassName = "brand-bar" }),
                    Span(new() { ClassName = "brand-dot" })),
                Span(new() { ClassName = "brand-name" },
                    Strong(new() { }, Text("Hieroglyph")),
                    Text(".NET"))),
            Each(Menus, menu => (Node)Menu(session, menu)),
            Div(new() { ClassName = "menu-spacer" }),
            Div(new() { ClassName = "menu-document" },
                Span(new() { ClassName = "menu-doc-family" }, Text(session.Project.FamilyName)),
                Span(new() { ClassName = "menu-doc-style" }, Text(session.Style.Name)),
                Span(new() { ClassName = "menu-doc-dot" }, Text("·")),
                Span(new() { ClassName = "menu-doc-count" },
                    Text($"{session.Project.DrawnCount} of {session.Project.CharacterCount} drawn"))));
    }

    private static Node Menu(FontEditor session, MenuDefinition definition)
    {
        bool open = session.OpenMenu == definition.Title;
        Ref<ElementNode> reference = new();
        ElementNode title = (ElementNode)Button(new()
        {
            ClassName = open ? "menu-title is-open" : "menu-title",
            OnClick = () =>
            {
                ElementNode? node = reference.Value;
                session.ToggleMenuAt(
                    definition.Title,
                    node?.PositionX ?? 0f,
                    (node?.PositionY ?? 0f) + (node?.Height ?? 30f) - 2f);
            },
        }, Text(definition.Title));
        title.BindRef(reference);

        title.On("mouseover", (Action)(() =>
        {
            ElementNode? node = reference.Value;
            session.HoverMenuAt(
                definition.Title,
                node?.PositionX ?? 0f,
                (node?.PositionY ?? 0f) + (node?.Height ?? 30f) - 2f);
        }));

        return Div(new() { ClassName = "menu" }, title);
    }

    internal static Node[] Rows(FontEditor session, MenuDefinition definition)
    {
        List<Node> rows = [];
        foreach (MenuAction item in definition.Items)
        {
            if (item.StartsGroup && rows.Count > 0)
            {
                rows.Add(Div(new() { ClassName = "menu-sep" }));
            }

            bool enabled = item.Enabled is null || item.Enabled(session);
            bool on = item.Checked is not null && item.Checked(session);
            rows.Add(Button(new()
            {
                ClassName = "menu-item",
                Disabled = !enabled,
                OnClick = () =>
                {
                    session.CloseMenu();
                    item.Run(session);
                },
            },
            Span(new() { ClassName = "menu-item-check" }, Show(on, Ui.Icon(Icons.Check))),
            Span(new() { ClassName = "menu-item-label" }, Text(item.Label)),
            Span(new() { ClassName = "menu-item-key" }, Text(item.Shortcut))));
        }

        return rows.ToArray();
    }

    private static MenuDefinition[] Build() =>
    [
        new("File",
        [
            new("New Font…", "Ctrl+N", session => session.OpenNewFont()),
            new("Open Starter Typeface", "", session => session.LoadSample()),
            new("Open Project…", "Ctrl+O", session => session.OpenProject(), StartsGroup: true),
            new("Save Project", "Ctrl+S", session => session.SaveProject()),
            new("Save Project As…", "", session => session.SaveProjectAs()),
            new("Export Font…", "Ctrl+E", session => session.OpenExport(), StartsGroup: true),
            new("Install Last Export", "", session => session.InstallLastExport(), session => session.LastExportPath is not null),
            new("Show Last Export in Folder", "", session => session.RevealExport(), session => session.LastExportPath is not null),
        ]),
        new("Edit",
        [
            new("Undo", "Ctrl+Z", session => session.Undo(), session => session.CanUndo),
            new("Redo", "Ctrl+Y", session => session.Redo(), session => session.CanRedo),
            new("Copy Character", "Ctrl+C", session => session.CopyGlyph(), session => session.SelectedGlyph?.HasOutline == true, StartsGroup: true),
            new("Paste Character", "Ctrl+V", session => session.PasteGlyph()),
            new("Duplicate Character…", "Ctrl+D", session => session.OpenDuplicateDialog(), session => session.SelectedGlyph?.HasOutline == true),
            new("Delete Points", "Del", session => session.DeleteSelection(), session => session.SelectedGlyph?.HasOutline == true, StartsGroup: true),
            new("Clear Character", "", session => session.ClearGlyph(), session => session.SelectedGlyph?.HasOutline == true),
            new("Select All Points", "Ctrl+A", session => session.SelectAllNodes(), session => session.SelectedGlyph?.HasOutline == true, StartsGroup: true),
            new("Deselect", "Esc", session =>
            {
                session.ClearSelection();
                session.Notify();
            }),
        ]),
        new("Glyph",
        [
            new("Previous Character", "[", session => session.StepGlyph(-1)),
            new("Next Character", "]", session => session.StepGlyph(1)),
            new("Add Character…", "", session => session.OpenAddCharacter(), StartsGroup: true),
            new("Add Ellipse", "", session => session.AddStarterEllipse(), StartsGroup: true),
            new("Add Rectangle", "", session => session.AddStarterRectangle()),
            new("Add Stem", "", session => session.AddStarterStem()),
            new("Flip Horizontal", "", session => session.FlipHorizontal(), session => session.SelectedGlyph?.HasOutline == true, StartsGroup: true),
            new("Flip Vertical", "", session => session.FlipVertical(), session => session.SelectedGlyph?.HasOutline == true),
            new("Centre Character", "", session => session.CentreGlyph(), session => session.SelectedGlyph?.HasOutline == true),
            new("Make Point Smooth", "", session => session.ToggleNodeSmooth(), session => session.SelectedNode >= 0, StartsGroup: true),
            new("Delete Character…", "", session => session.RemoveCharacter(session.SelectedCode), session => session.Style.Glyphs.Count > 1),
        ]),
        new("View",
        [
            new("Zoom In", "Ctrl+=", session => session.ZoomStep(1)),
            new("Zoom Out", "Ctrl+-", session => session.ZoomStep(-1)),
            new("Fit Glyph", "Ctrl+0", session => session.FitGlyph()),
            new("Show Grid", "", session => session.ToggleGrid(), Checked: session => session.ShowGrid, StartsGroup: true),
            new("Show Metrics", "", session => session.ToggleMetrics(), Checked: session => session.ShowMetrics),
            new("Show Sidebearings", "", session => session.ToggleBearings(), Checked: session => session.ShowBearings),
            new("Character Browser", "", session => session.ToggleBrowser(), Checked: session => session.ShowBrowser, StartsGroup: true),
            new("Inspector", "", session => session.ToggleInspector(), Checked: session => session.ShowInspector),
            new("Preview Panel", "", session => session.TogglePreview(), Checked: session => session.ShowPreview),
            new("Specimen Viewer…", "", session => session.OpenSpecimen(), StartsGroup: true),
        ]),
        new("Font",
        [
            new("Styles and Weights…", "", session => session.OpenStyles()),
            new("Kerning", "", session =>
            {
                if (!session.ShowInspector)
                {
                    session.ToggleInspector();
                }

                session.NotifyStatus("Kerning is in the inspector on the right.");
            }),
            new("Add Character…", "", session => session.OpenAddCharacter(), StartsGroup: true),
        ]),
        new("Help",
        [
            new("Keyboard Shortcuts", "", session => session.OpenShortcuts()),
            new("Welcome Screen", "", session => session.OpenWelcome()),
            new("About Hieroglyph.NET", "", session => session.OpenAbout(), StartsGroup: true),
        ]),
    ];
}
