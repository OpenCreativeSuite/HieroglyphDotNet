using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>The left column: a navigable character browser.</summary>
internal sealed class CharacterBrowser : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        List<Glyph> characters = session.FilteredCharacters();
        List<Node> chips = [Ui.Chip("All", session.BrowserCategory is null, () =>
        {
            session.BrowserCategory = null;
            session.Notify();
        }, "Every character", session.Project.CharacterCount.ToString())];

        foreach (CharacterCategory category in CharacterSet.Order)
        {
            (int drawn, int total) = session.CategoryProgress(category);
            if (total == 0 && category == CharacterCategory.Custom)
            {
                continue;
            }

            CharacterCategory local = category;
            chips.Add(Ui.Chip(
                CharacterSet.Label(category),
                session.BrowserCategory == category,
                () =>
                {
                    session.BrowserCategory = session.BrowserCategory == local ? null : local;
                    session.Notify();
                },
                $"{drawn} of {total} drawn",
                $"{drawn}/{total}"));
        }

        return Div(new() { ClassName = "browser" },
            Div(new() { ClassName = "browser-head" },
                Span(new() { ClassName = "browser-title" }, Text("Characters")),
                Span(new() { ClassName = "browser-progress" },
                    Text($"{session.Project.DrawnCount} / {session.Project.CharacterCount}"))),
            Div(new() { ClassName = "browser-search" },
                Span(new() { ClassName = "browser-search-icon" }, Ui.Icon(Icons.Zoom)),
                Input(new()
                {
                    ClassName = "browser-search-input",
                    Type = "search",
                    Placeholder = "Search letter, name or U+0041",
                    Value = session.BrowserSearch,
                    OnInput = value =>
                    {
                        session.BrowserSearch = value;
                        session.Notify();
                    },
                    OnFocus = Ui.Focus(session),
                    OnBlur = Ui.Blur(session),
                })),
            Div(new() { ClassName = "browser-chips" }, chips.ToArray()),
            Show(characters.Count == 0,
                Div(new() { ClassName = "browser-empty" },
                    Ui.Icon(Icons.Zoom, "browser-empty-icon"),
                    Text("No characters match that search."))),
            Show(characters.Count > 0,
                Div(new() { ClassName = "browser-grid" },
                    Each(characters, glyph => (Node)C<CharacterTile, CharacterTileProps>(
                        new CharacterTileProps { Code = glyph.Codepoint, Key = "char-" + glyph.Codepoint })))),
            Div(new() { ClassName = "browser-foot" },
                Button(new()
                {
                    ClassName = "browser-add",
                    Title = "Add a character from any Unicode block",
                    OnClick = session.OpenAddCharacter,
                }, Ui.Icon(Icons.Plus), Span(new() { ClassName = "browser-add-label" }, Text("Add character"))),
                Button(new()
                {
                    ClassName = session.BrowserOnlyBlank ? "browser-filter is-on" : "browser-filter",
                    Title = "Show only characters that still need drawing",
                    OnClick = () =>
                    {
                        session.BrowserOnlyBlank = !session.BrowserOnlyBlank;
                        session.Notify();
                    },
                }, Ui.Icon(Icons.EyeOff))));
    }
}

/// <summary>Props for one character tile.</summary>
internal sealed class CharacterTileProps : Props
{
    /// <summary>The code point shown.</summary>
    public int Code { get; init; }
}

/// <summary>One clickable character with a live miniature of its outline.</summary>
internal sealed class CharacterTile : Component<CharacterTileProps>
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? glyph = session.Style.FindGlyph(Props.Code);
        bool selected = session.SelectedCode == Props.Code;
        bool drawn = glyph?.HasOutline == true;
        int code = Props.Code;
        string title = glyph is null
            ? "U+" + code.ToString("X4")
            : $"{glyph.Name}  ·  U+{code:X4}" + (drawn ? "" : "  ·  still blank");

        List<string> classes = ["char-tile"];
        classes.Add(selected ? "is-selected" : "");
        classes.Add(drawn ? "is-drawn" : "is-blank");

        return Div(new()
        {
            ClassName = string.Join(' ', classes.Where(name => name.Length > 0)),
            Title = title,
            OnClick = () => session.SelectGlyph(code),
        },
        Canvas(new CanvasProps
        {
            ClassName = "tile-canvas",
            Attributes = Ui.Size(54, 48),
            OnDraw = context => Draw(context, session, code, drawn),
        }),
        Span(new() { ClassName = "tile-dot" }));
    }

    private static void Draw(CanvasContext context, FontEditor session, int code, bool drawn)
    {
        Glyph? glyph = session.Style.FindGlyph(code);
        if (glyph is null)
        {
            return;
        }

        Rect box = new(0f, 0f, 54f, 48f);
        if (drawn)
        {
            uint colour = session.SelectedCode == code ? 0xFFFFF2E6u : 0xFFCBD2DCu;
            Typeface.DrawInBox(context, session.RasterCache, session.Style, glyph, box, colour, 0.76f);
            return;
        }

        TextStyle style = new(
            "Segoe UI",
            19f,
            400,
            false,
            0xFF5B6470,
            0f,
            22f,
            TextAlign.Center,
            TextDecoration.None,
            WhiteSpace.NoWrap,
            Direction.LeftToRight);

        context.DrawText(new Rect(0f, 12f, 54f, 24f), glyph.Character.Length > 0 ? glyph.Character : "·", style);
    }
}
