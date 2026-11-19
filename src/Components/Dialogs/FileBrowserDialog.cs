using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>In-app file browser for projects and font exports.</summary>
internal sealed class FileBrowserDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);

        return Div(new() { ClassName = "modal file-browser", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text(session.BrowserTitle)),
                Ui.IconButton(Icons.Close, "Close", session.CloseFileBrowser)),
            Div(new() { ClassName = "file-toolbar" },
                Div(new() { ClassName = "file-path-row" },
                    Ui.IconButton(Icons.ArrowUp, "Parent folder", session.BrowserGoUp),
                    Input(new()
                    {
                        ClassName = "file-path",
                        Type = "text",
                        Value = session.BrowserFolder,
                        Placeholder = "Folder path; press Enter to open",
                        OnChange = session.NavigateBrowser,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    })),
                Div(new() { ClassName = "file-places" },
                    Each(session.BrowserPlaces, place => (Node)Button(new()
                    {
                        ClassName = "file-place",
                        Title = place.Path,
                        OnClick = () => session.BrowserUsePlace(place.Path),
                    }, Text(place.Label))))),
            Show(session.BrowserEntries.Count == 0,
                Div(new() { ClassName = "file-empty" },
                    Text(session.BrowserSaveMode
                        ? "No matching files here yet. Type a name below to create one."
                        : "No project files in this folder."))),
            Show(session.BrowserEntries.Count > 0,
                Div(new() { ClassName = "file-list" },
                    Each(session.BrowserEntries, entry => (Node)Row(session, entry)))),
            Div(new() { ClassName = "file-foot" },
                Div(new() { ClassName = "file-name-row" },
                    Span(new() { ClassName = "file-label" }, Text("File name")),
                    Input(new()
                    {
                        ClassName = "file-name",
                        Type = "text",
                        Value = session.BrowserName,
                        Placeholder = session.BrowserSaveMode ? "MyTypeface" : "Choose a file above",
                        OnInput = value => session.BrowserName = value,
                        OnChange = value => session.BrowserName = value,
                        OnFocus = Ui.Focus(session),
                        OnBlur = Ui.Blur(session),
                    })),
                Show(session.BrowserStatus.Length > 0,
                    Span(new() { ClassName = "file-status" }, Text(session.BrowserStatus))),
                Div(new() { ClassName = "file-actions" },
                    Button(new() { ClassName = "btn", OnClick = session.CloseFileBrowser }, Text("Cancel")),
                    Button(new() { ClassName = "btn is-primary", OnClick = session.BrowserConfirmEntry }, Text(session.BrowserConfirm)))));
    }

    private static Node Row(FontEditor session, FileEntry entry)
    {
        bool selected = !entry.IsFolder && string.Equals(entry.Path, session.BrowserSelected, StringComparison.OrdinalIgnoreCase);
        return Div(new()
        {
            ClassName = selected ? "file-row is-selected" : "file-row",
            Title = entry.Path,
            OnClick = () => session.BrowserClickEntry(entry),
        },
        Ui.Icon(entry.IsFolder ? Icons.Folder : Icons.Document, "file-row-icon"),
        Span(new() { ClassName = "file-row-name" }, Text(entry.Name)),
        Span(new() { ClassName = "file-row-size" }, Text(entry.IsFolder ? "Folder" : Size(entry.Size))));
    }

    private static string Size(long bytes) => bytes < 1024
        ? bytes + " B"
        : bytes < 1024 * 1024
            ? (bytes / 1024f).ToString("0.#") + " KB"
            : (bytes / (1024f * 1024f)).ToString("0.#") + " MB";
}
