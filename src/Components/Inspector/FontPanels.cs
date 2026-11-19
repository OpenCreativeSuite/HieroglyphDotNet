using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Metric guides: the vertical rulers every glyph is drawn against.</summary>
internal sealed class MetricsPanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        FontMetrics metrics = session.Style.Metrics;

        return C<Panel, PanelProps>(new PanelProps
        {
            Title = "Metrics",
            Badge = $"{metrics.UnitsPerEm} upm",
            StartOpen = false,
            Children =
            [
                Div(new() { ClassName = "field-grid" },
                    Field(session, "UPM", metrics.UnitsPerEm, "unitsPerEm", "Units per em"),
                    Field(session, "Asc", metrics.Ascender, "ascender", "Ascender"),
                    Field(session, "Desc", metrics.Descender, "descender", "Descender"),
                    Field(session, "Cap", metrics.CapHeight, "cap", "Cap height"),
                    Field(session, "x", metrics.XHeight, "xheight", "x-height"),
                    Field(session, "Gap", metrics.LineGap, "linegap", "Line gap")),
                Ui.Hint("The em square is the design grid. Cap height and x-height place the flat guides you draw against; ascender and descender set the line box."),
                Div(new() { ClassName = "panel-actions" },
                    Button(new()
                    {
                        ClassName = "btn-tiny",
                        Title = "Reset the metric guides to 1000 upm defaults",
                        OnClick = () =>
                        {
                            session.PushUndo();
                            metrics.Ascender = 800;
                            metrics.Descender = -200;
                            metrics.CapHeight = 700;
                            metrics.XHeight = 500;
                            metrics.LineGap = 0;
                            session.Touch();
                        },
                    }, Text("Reset guides")),
                    Button(new()
                    {
                        ClassName = "btn-tiny",
                        Title = "Show or hide the metric guides",
                        OnClick = session.ToggleMetrics,
                    }, Text(session.ShowMetrics ? "Guides on" : "Guides off"))),
            ],
        });
    }

    private static Node Field(FontEditor session, string key, int value, string metric, string title) =>
        Ui.NumField(session, key, value, text =>
        {
            int parsed = Ui.ParseInt(text, value);
            if (metric == "unitsPerEm")
            {
                session.SetUnitsPerEm(parsed);
            }
            else
            {
                session.PushUndo();
                session.SetMetric(metric, parsed);
            }
        }, title);
}

/// <summary>Family metadata and the style list.</summary>
internal sealed class FontPanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        FontStyle style = session.Style;

        return F(
            C<Panel, PanelProps>(new PanelProps
            {
                Title = "Font",
                Badge = session.Project.FamilyName,
                Children =
                [
                    Field(session, "Family name", session.Project.FamilyName, value => session.SetFamilyField("family", value)),
                    Field(session, "Style name", style.Name, session.RenameStyle),
                    Div(new() { ClassName = "field-label" }, Text("Weight")),
                    Div(new() { ClassName = "choice-row" },
                        WeightChip(session, "Light", 300),
                        WeightChip(session, "Regular", 400),
                        WeightChip(session, "Medium", 500),
                        WeightChip(session, "Bold", 700)),
                    Div(new() { ClassName = "field-label" }, Text("Width")),
                    Div(new() { ClassName = "choice-row" },
                        WidthChip(session, "Condensed", 3),
                        WidthChip(session, "Normal", 5),
                        WidthChip(session, "Expanded", 7)),
                    Div(new() { ClassName = "field-grid" },
                        Ui.NumField(session, "Wt", style.Weight, value =>
                        {
                            style.Weight = Math.Clamp(Ui.ParseInt(value, style.Weight), 100, 900);
                            session.MarkDirty();
                        }, "Weight class"),
                        Ui.NumField(session, "Wd", style.WidthClass, value =>
                        {
                            style.WidthClass = Math.Clamp(Ui.ParseInt(value, style.WidthClass), 1, 9);
                            session.MarkDirty();
                        }, "Width class")),
                    Div(new() { ClassName = "panel-actions" },
                        Button(new() { ClassName = "btn-tiny", OnClick = session.OpenStyles },
                            Ui.Icon(Icons.Layers), Text("Manage styles")),
                        Button(new()
                        {
                            ClassName = style.Italic ? "btn-tiny is-on" : "btn-tiny",
                            OnClick = () =>
                            {
                                style.Italic = !style.Italic;
                                session.MarkDirty();
                            },
                        }, Text("Italic"))),
                    Div(new() { ClassName = "panel-divider" }),
                    Field(session, "Version", session.Project.Version, value => session.SetFamilyField("version", value)),
                    Field(session, "Designer", session.Project.Designer, value => session.SetFamilyField("designer", value)),
                    Field(session, "Description", session.Project.Description, value => session.SetFamilyField("description", value), multiline: true),
                    Field(session, "Copyright", session.Project.Copyright, value => session.SetFamilyField("copyright", value), multiline: true),
                    Field(session, "Licence", session.Project.License, value => session.SetFamilyField("license", value)),
                ],
            }));
    }

    private static Node WeightChip(FontEditor session, string label, int weight) =>
        Button(new()
        {
            ClassName = session.Style.Weight == weight ? "choice is-active" : "choice",
            OnClick = () =>
            {
                session.Style.Weight = weight;
                session.MarkDirty();
            },
        }, Text(label));

    private static Node WidthChip(FontEditor session, string label, int width) =>
        Button(new()
        {
            ClassName = session.Style.WidthClass == width ? "choice is-active" : "choice",
            OnClick = () =>
            {
                session.Style.WidthClass = width;
                session.MarkDirty();
            },
        }, Text(label));

    private static Node Field(FontEditor session, string label, string value, Action<string> commit, bool multiline = false) =>
        Div(new() { ClassName = "field-stack" },
            Div(new() { ClassName = "field-label" }, Text(label)),
            multiline
                ? Ui.TextArea(session, value, commit)
                : Ui.TextField(session, value, commit));
}
