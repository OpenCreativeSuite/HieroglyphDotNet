using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Compile, validate, save, install and preview a finished font.</summary>
internal sealed class ExportDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        var (installMessage, setInstallMessage) = State("");

        Node body = session.ExportComplete
            ? Success(session, installMessage)
            : Options(session);

        return Div(new() { ClassName = "modal export-modal", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" },
                    Text(session.ExportComplete ? "Font exported" : "Export font")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body" }, body),
            Div(new() { ClassName = "modal-actions" },
                session.ExportComplete
                    ? F(
                        Button(new()
                        {
                            ClassName = "btn",
                            OnClick = () =>
                            {
                                session.OpenExport();
                                setInstallMessage("");
                            },
                        }, Text("Export another")),
                        Button(new()
                        {
                            ClassName = "btn",
                            OnClick = () =>
                            {
                                session.CloseDialog();
                                session.OpenSpecimen();
                            },
                        }, Text("Open specimen")),
                        Button(new()
                        {
                            ClassName = "btn is-primary",
                            OnClick = () =>
                            {
                                setInstallMessage(session.InstallLastExport());
                                session.Notify();
                            },
                        }, Ui.Icon(Icons.Download), Text("Install font")),
                        Button(new() { ClassName = "btn", OnClick = session.CloseDialog }, Text("Done")))
                    : F(
                        Button(new() { ClassName = "btn", OnClick = session.CloseDialog }, Text("Cancel")),
                        Button(new() { ClassName = "btn is-primary", OnClick = session.BeginExport }, Text("Choose location and export")))));
    }

    private static Node Options(FontEditor session)
    {
        List<Node> formats = [];
        foreach (FontFormat format in Enum.GetValues<FontFormat>())
        {
            FontFormat local = format;
            bool active = session.ExportFormat == format;
            formats.Add(Div(new()
            {
                ClassName = active ? "format-card is-active" : "format-card",
                Title = FontExporter.Describe(format),
                OnClick = () => session.SetExportFormat(local),
            },
            Div(new() { ClassName = "format-head" },
                Span(new() { ClassName = "format-ext" }, Text(FontExporter.ShortLabel(format))),
                Span(new() { ClassName = "format-label" }, Text(FontExporter.Label(format))),
                Show(active, Span(new() { ClassName = "format-check" }, Ui.Icon(Icons.Check)))),
            Div(new() { ClassName = "format-copy" }, Text(FontExporter.Describe(format)))));
        }

        List<Node> rows = [];
        foreach (FontIssue issue in session.ExportIssues)
        {
            string marker = issue.Level switch
            {
                IssueLevel.Error => "is-error",
                IssueLevel.Warning => "is-warning",
                _ => "is-info",
            };

            rows.Add(Div(new() { ClassName = "issue-row" },
                Span(new() { ClassName = "issue-dot " + marker }),
                Span(new() { ClassName = "issue-message" }, Text(issue.Message)),
                Show(issue.Detail is { Length: > 0 },
                    Span(new() { ClassName = "issue-detail" }, Text(issue.Detail ?? "")))));
        }

        return F(
            Div(new() { ClassName = "field-label" }, Text("Format")),
            Div(new() { ClassName = "format-grid" }, formats.ToArray()),
            Show(session.Project.Styles.Count > 1,
                F(
                    Div(new() { ClassName = "field-label" }, Text("Style")),
                    Div(new() { ClassName = "choice-row" },
                        Each(session.Project.Styles, style => (Node)Button(new()
                        {
                            ClassName = session.ExportStyleIndex == session.Project.Styles.IndexOf(style)
                                ? "choice is-active"
                                : "choice",
                            OnClick = () => session.SetExportStyle(session.Project.Styles.IndexOf(style)),
                        }, Text(style.Name)))))),
            Div(new() { ClassName = "field-stack" },
                Div(new() { ClassName = "field-label" }, Text("File name")),
                Input(new()
                {
                    ClassName = "text-field-wide",
                    Type = "text",
                    Value = session.ExportName,
                    OnInput = value => session.ExportName = value,
                    OnChange = value => session.ExportName = value,
                    OnFocus = Ui.Focus(session),
                    OnBlur = Ui.Blur(session),
                })),
            Div(new() { ClassName = "field-label" }, Text($"Before you export · {session.ExportIssues.Count} note{(session.ExportIssues.Count == 1 ? "" : "s")}")),
            Div(new() { ClassName = "issue-list" }, rows.ToArray()),
            Div(new() { ClassName = "export-note" },
                Ui.Icon(Icons.Info),
                Div(new() { ClassName = "export-note-text" },
                    Text("Nothing here blocks a beginner. Blank characters are simply left out of the font, and you can always export again later."))));
    }

    private static Node Success(FontEditor session, string installMessage)
    {
        string path = session.LastExportPath ?? "";
        return F(
            Div(new() { ClassName = "export-success" },
                Div(new() { ClassName = "export-check" }, Ui.Icon(Icons.Check)),
                Div(new() { ClassName = "export-success-text" },
                    Div(new() { ClassName = "export-success-title" },
                        Text($"{session.Project.FamilyName} {session.Style.Name} is ready")),
                    Div(new() { ClassName = "export-success-file" }, Text(session.ExportMessage)),
                    Div(new() { ClassName = "export-success-path" }, Text(path)))),
            Div(new() { ClassName = "export-next" },
                Div(new() { ClassName = "export-next-title" }, Text("Put it to work")),
                Div(new() { ClassName = "export-next-copy" },
                    Text("Install copies the file into your personal font collection, so every application can use it. Web fonts can be dropped straight onto a site.")),
                Show(installMessage.Length > 0,
                    Div(new() { ClassName = "export-install" }, Text(installMessage))),
                Div(new() { ClassName = "panel-actions" },
                    Button(new() { ClassName = "btn-tiny", OnClick = session.RevealExport }, Text("Show in folder")),
                    Button(new()
                    {
                        ClassName = "btn-tiny",
                        OnClick = () =>
                        {
                            session.CloseDialog();
                            session.OpenSpecimen();
                        },
                    }, Text("Preview specimen")))),
            Div(new() { ClassName = "export-formats" },
                Text("The same outlines were written once. Re-export any time in TTF, OTF, WOFF or WOFF2.")));
    }
}
