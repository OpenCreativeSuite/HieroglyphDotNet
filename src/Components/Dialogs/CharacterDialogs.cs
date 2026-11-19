using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Copies the current outline into another character.</summary>
internal sealed class DuplicateDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        Glyph? source = session.SelectedGlyph;
        var (onlyEmpty, setOnlyEmpty) = State(true);

        if (source is null)
        {
            return F();
        }

        List<Glyph> targets = session.Style.Glyphs.Values
            .Where(glyph => glyph.Codepoint != source.Codepoint)
            .Where(glyph => !onlyEmpty || !glyph.HasOutline)
            .OrderBy(glyph => Array.IndexOf(CharacterSet.Order, CharacterSet.CategoryFor(glyph.Codepoint)))
            .ThenBy(glyph => glyph.Codepoint)
            .ToList();

        return Div(new() { ClassName = "modal duplicate-modal", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" },
                    Text($"Copy {source.Character} to another character")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body" },
                Div(new() { ClassName = "panel-hint" },
                    Text($"The outline and advance width of {source.Name} will be copied. The target becomes an independent character you can reshape — nothing is linked.")),
                Div(new() { ClassName = "choice-row" },
                    Button(new()
                    {
                        ClassName = onlyEmpty ? "choice is-active" : "choice",
                        OnClick = () => setOnlyEmpty(true),
                    }, Text("Empty characters")),
                    Button(new()
                    {
                        ClassName = onlyEmpty ? "choice" : "choice is-active",
                        OnClick = () => setOnlyEmpty(false),
                    }, Text("All characters"))),
                Div(new() { ClassName = "duplicate-grid" },
                    Each(targets, target => (Node)Button(new()
                    {
                        ClassName = target.HasOutline ? "dup-cell is-full" : "dup-cell",
                        Title = $"{target.Name} · U+{target.Codepoint:X4}" + (target.HasOutline ? " · will be replaced" : " · empty"),
                        OnClick = () => session.DuplicateTo(target.Codepoint),
                    }, Text(target.Character)))),
                Div(new() { ClassName = "panel-hint" },
                    Text("Tip: O → o, or I → l → 1 is how most typefaces begin."))),
            Div(new() { ClassName = "modal-actions" },
                Button(new() { ClassName = "btn", OnClick = session.CloseDialog }, Text("Cancel"))));
    }
}

/// <summary>Adds a character from anywhere in Unicode.</summary>
internal sealed class AddCharacterDialog : Component
{
    private static readonly string[] Suggestions =
    [
        "é", "è", "ü", "ñ", "ç", "å", "ø", "æ", "œ", "ß", "£", "€", "°", "±", "×",
        "§", "¶", "†", "‡", "•", "…", "“", "”", "‘", "’", "–", "—", "←", "→", "★", "♥",
    ];

    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        var (input, setInput) = State("é");
        int codepoint = Parse(input);
        string name = codepoint > 0 ? CharacterSet.NameFor(codepoint) : "Type a character or a code point";
        bool exists = codepoint > 0 && session.Style.Glyphs.ContainsKey(codepoint);

        Node preview = Div(new() { ClassName = "addchar-preview" },
            Text(codepoint > 0 ? char.ConvertFromUtf32(codepoint) : "?"));

        Node fields = Div(new() { ClassName = "addchar-fields" },
            Input(new()
            {
                ClassName = "text-field-wide is-large",
                Type = "text",
                Value = input,
                Placeholder = "é or U+00E9",
                OnInput = setInput,
                OnChange = setInput,
                OnFocus = Ui.Focus(session),
                OnBlur = Ui.Blur(session),
            }),
            Div(new() { ClassName = "addchar-name" }, Text(name)),
            Div(new() { ClassName = "addchar-code" },
                Text(codepoint > 0
                    ? $"U+{codepoint:X4}  ·  decimal {codepoint}"
                    : "Letters, digits, punctuation, symbols or any code point")));

        Node row = Div(new() { ClassName = "addchar-row" }, preview, fields);

        Node suggestions = Div(new() { ClassName = "addchar-suggestions" },
            Each(Suggestions, suggestion => (Node)Button(new()
            {
                ClassName = session.Style.Glyphs.ContainsKey(Parse(suggestion))
                    ? "dup-cell is-full"
                    : "dup-cell",
                OnClick = () => setInput(suggestion),
            }, Text(suggestion))));

        Node? note = Show(codepoint > 0 && exists,
            Div(new() { ClassName = "addchar-exists" },
                Ui.Icon(Icons.Info),
                Text("That character is already in the font. Adding it will simply select it.")));

        Node body = Div(new() { ClassName = "modal-body" },
            row,
            Div(new() { ClassName = "field-label" }, Text("Common additions")),
            suggestions,
            note);

        Node actions = Div(new() { ClassName = "modal-actions" },
            Button(new() { ClassName = "btn", OnClick = session.CloseDialog }, Text("Cancel")),
            Button(new()
            {
                ClassName = "btn is-primary",
                Disabled = codepoint <= 0,
                OnClick = () => session.AddCharacter(codepoint),
            }, Text(exists ? "Select character" : "Add character")));

        return Div(new() { ClassName = "modal addchar-modal", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text("Add a character")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            body,
            actions);
    }

    private static int Parse(string text)
    {
        string clean = text.Trim();
        if (clean.Length == 0)
        {
            return 0;
        }

        if (clean.StartsWith("U+", StringComparison.OrdinalIgnoreCase)
            || clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            string hex = clean[2..].Trim();
            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        if (clean.Length is 4 or 5 or 6
            && int.TryParse(clean, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int hexOnly)
            && hexOnly > 0x7F)
        {
            return hexOnly;
        }

        try
        {
            return char.ConvertToUtf32(clean, 0);
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>What this application is and what it believes in.</summary>
internal sealed class AboutDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);

        return Div(new() { ClassName = "modal about-modal is-bare", OnClick = () => { } },
            Div(new() { ClassName = "modal-head" },
                Span(new() { ClassName = "modal-title" }, Text("About Hieroglyph.NET")),
                Ui.IconButton(Icons.Close, "Close", session.CloseDialog)),
            Div(new() { ClassName = "modal-body about-body" },
                Div(new() { ClassName = "about-mark" },
                    Div(new() { ClassName = "brand-mark is-large" },
                        Span(new() { ClassName = "brand-bar" }),
                        Span(new() { ClassName = "brand-dot" }))),
                Div(new() { ClassName = "about-name" }, Text("Hieroglyph.NET")),
                Div(new() { ClassName = "about-version" }, Text("A typeface design application for the Open Creative Suite")),
                P(new() { ClassName = "about-copy" },
                    Text("Hieroglyph exists so that making a font feels as immediate as drawing a picture. Sketch a letter, nudge a curve, preview the alphabet, export a real font file — no build tools, no compiler flags, no accounts.")),
                Div(new() { ClassName = "about-values" },
                    AboutValue("Local by design", "Projects are plain files on your disk. Nothing is uploaded, ever."),
                    AboutValue("Open formats", "Export TTF, OTF, WOFF and WOFF2 — files that belong to you."),
                    AboutValue("No throwaway controls", "Everything in the interface does something real.")),
                Div(new() { ClassName = "about-foot" },
                    Text("Built with Donkeh UI  ·  Icons from Font Awesome Free  ·  Version 1.0"))));
    }

    private static Node AboutValue(string title, string copy) =>
        Div(new() { ClassName = "about-value" },
            Div(new() { ClassName = "about-value-title" }, Text(title)),
            Div(new() { ClassName = "about-value-copy" }, Text(copy)));
}
