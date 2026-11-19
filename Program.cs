using Donkeh.Hosting;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet;

/// <summary>
/// Application root. Owns the editing session and publishes it to the chrome,
/// the canvas, the inspector and the preview; a tick counter rebuilds the tree
/// whenever the model reports a change.
/// </summary>
[AppRoot(
    Width = 1440,
    Height = 920,
    Title = "Hieroglyph.NET",
    ClearColor = 0xFF0E1013,
    MinWidth = 1080,
    MinHeight = 680)]
public sealed class App : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        Ref<FontEditor> reference = UseRef<FontEditor>();
        FontEditor session = reference.Value ??= new FontEditor();
        var (_, _, tick) = StateWithUpdate(0);
        session.Refresh = () => tick(value => value + 1);

        List<string> classes = ["app"];
        if (!session.ShowBrowser)
        {
            classes.Add("is-browser-hidden");
        }

        if (!session.ShowInspector)
        {
            classes.Add("is-inspector-hidden");
        }

        if (!session.ShowPreview)
        {
            classes.Add("is-preview-hidden");
        }

        Node root = Div(new()
        {
            ClassName = string.Join(' ', classes),
            OnClick = () =>
            {
                if (session.OpenMenu is not null)
                {
                    session.CloseMenu();
                }
            },
            OnKeyDown = chord => session.HandleKey(chord),
        },
        C<MenuBar>(),
        C<Toolbar>(),
        C<CharacterBrowser>(),
        C<Workspace>(),
        C<InspectorPanel>(),
        C<PreviewDock>(),
        C<StatusBar>(),
        Show(session.OpenMenu is not null, C<MenuOverlay>()),
        Show(session.ShowWelcome, Div(new() { ClassName = "welcome-layer" }, C<WelcomeDialog>())),
        Show(session.ShowNewFont, Layer("modal-layer", C<NewFontDialog>(), session.CloseDialog)),
        Show(session.ShowExport, Layer("modal-layer", C<ExportDialog>(), session.CloseDialog)),
        Show(session.ShowSpecimen, Layer("modal-layer is-wide", C<SpecimenDialog>(), session.CloseDialog)),
        Show(session.ShowStyles, Layer("modal-layer", C<StylesDialog>(), session.CloseDialog)),
        Show(session.ShowShortcuts, Layer("modal-layer", C<ShortcutsDialog>(), session.CloseDialog)),
        Show(session.ShowDuplicate, Layer("modal-layer", C<DuplicateDialog>(), session.CloseDialog)),
        Show(session.ShowAddCharacter, Layer("modal-layer", C<AddCharacterDialog>(), session.CloseDialog)),
        Show(session.ShowAbout, Layer("modal-layer", C<AboutDialog>(), session.CloseDialog)),
        Show(session.ShowFileBrowser, Layer("modal-layer", C<FileBrowserDialog>(), session.CloseFileBrowser)));

        return Provider(SessionContext.Value, session, root);
    }

    private static Node Layer(string className, Node child, Action close) =>
        Div(new() { ClassName = className, OnClick = close }, child);
}
