using System.Globalization;
using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>Markup helpers shared by the chrome, the canvas and the inspector.</summary>
internal static class Ui
{
    /// <summary>Formats a number without trailing noise.</summary>
    public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Formats a whole number.</summary>
    public static string Whole(float value) => MathF.Round(value).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Parses a float, falling back to a default.</summary>
    public static float Parse(string text, float fallback) => FontEditor.Parse(text, fallback);

    /// <summary>Parses a whole number.</summary>
    public static int ParseInt(string text, int fallback) => FontEditor.ParseInt(text, fallback);

    /// <summary>Focus handler that suspends global shortcuts.</summary>
    public static Action Focus(FontEditor session) => () => session.IsTyping = true;

    /// <summary>Blur handler that restores global shortcuts.</summary>
    public static Action Blur(FontEditor session) => () => session.IsTyping = false;

    /// <summary>A Font Awesome glyph.</summary>
    public static Node Icon(string name, string extra = "") =>
        I(new() { ClassName = $"fa {name} {extra}".TrimEnd() });

    /// <summary>A square icon button.</summary>
    public static Node IconButton(
        string icon,
        string title,
        Action onClick,
        bool enabled = true,
        bool active = false,
        string className = "icon-btn")
    {
        List<string> classes = [className];
        if (active)
        {
            classes.Add("is-on");
        }

        return Button(new()
        {
            ClassName = string.Join(' ', classes),
            Title = title,
            Disabled = !enabled,
            OnClick = onClick,
        },
        Icon(icon));
    }

    /// <summary>A small square button with an icon.</summary>
    public static Node MiniButton(string icon, string title, Action onClick, bool active = false, bool enabled = true) =>
        IconButton(icon, title, onClick, enabled, active, "mini-btn");

    /// <summary>A labelled toolbar action.</summary>
    public static Node ActionButton(string icon, string label, string title, Action onClick, bool primary = false) =>
        Button(new()
        {
            ClassName = primary ? "tb-action is-primary" : "tb-action",
            Title = title,
            OnClick = onClick,
        },
        Icon(icon),
        Span(new() { ClassName = "tb-action-label" }, Text(label)));

    /// <summary>A numeric field with a short key.</summary>
    public static Node NumField(FontEditor session, string key, float value, Action<string> commit, string title = "")
    {
        ElementNode node = (ElementNode)Div(new() { ClassName = "field-row", Title = title.Length > 0 ? title : key },
            Span(new() { ClassName = "field-key" }, Text(key)),
            Input(new()
            {
                ClassName = "num-field",
                Type = "text",
                Value = Number(MathF.Round(value * 100f) / 100f),
                OnChange = commit,
                OnFocus = Focus(session),
                OnBlur = Blur(session),
            }));
        return node;
    }

    /// <summary>A numeric field filling its row.</summary>
    public static Node WideNumField(FontEditor session, string value, Action<string> commit) =>
        Input(new()
        {
            ClassName = "text-field-wide",
            Type = "text",
            Value = value,
            OnChange = commit,
            OnFocus = Focus(session),
            OnBlur = Blur(session),
        });

    /// <summary>A labelled text field.</summary>
    public static Node TextField(FontEditor session, string value, Action<string> commit, string placeholder = "") =>
        Input(new()
        {
            ClassName = "text-field-wide",
            Type = "text",
            Value = value,
            Placeholder = placeholder,
            OnChange = commit,
            OnFocus = Focus(session),
            OnBlur = Blur(session),
        });

    /// <summary>A multi-line text field.</summary>
    public static Node TextArea(FontEditor session, string value, Action<string> commit, int height = 56) =>
        Textarea(new()
        {
            ClassName = "text-area",
            Value = value,
            OnChange = commit,
            OnFocus = Focus(session),
            OnBlur = Blur(session),
            Attributes = new() { ["rows"] = (height / 18).ToString(CultureInfo.InvariantCulture) },
        });

    /// <summary>A labelled grouped control inside the action bar.</summary>
    public static Node OptionsGroup(params Node[] children) =>
        Div(new() { ClassName = "options-group" }, children);

    /// <summary>A vertical divider in the action bar.</summary>
    public static Node OptionsSeparator() => Div(new() { ClassName = "options-sep" });

    /// <summary>Builds canvas width/height attributes.</summary>
    public static Dictionary<string, string> Size(int width, int height) => new()
    {
        ["width"] = width.ToString(CultureInfo.InvariantCulture),
        ["height"] = height.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>A labelled slider.</summary>
    public static Node Slider(
        FontEditor session,
        string label,
        float value,
        float min,
        float max,
        string display,
        Action<string> onInput,
        Action<string>? onChange = null)
    {
        return Div(new() { ClassName = "slider-row" },
            Div(new() { ClassName = "slider-head" },
                Span(new() { ClassName = "slider-label" }, Text(label)),
                Span(new() { ClassName = "slider-value" }, Text(display))),
            Input(new()
            {
                ClassName = "slider-input",
                Type = "range",
                Value = Number(value),
                Attributes = new()
                {
                    ["min"] = Number(min),
                    ["max"] = Number(max),
                    ["step"] = Number((max - min) / 200f),
                },
                OnInput = onInput,
                OnChange = onChange ?? onInput,
                OnFocus = Focus(session),
                OnBlur = Blur(session),
            }));
    }

    /// <summary>A small pill button.</summary>
    public static Node Chip(string label, bool active, Action onClick, string title = "", string badge = "")
    {
        List<Node> children = [Span(new() { ClassName = "chip-label" }, Text(label))];
        if (badge.Length > 0)
        {
            children.Add(Span(new() { ClassName = "chip-badge" }, Text(badge)));
        }

        return Button(new()
        {
            ClassName = active ? "chip is-active" : "chip",
            Title = title.Length > 0 ? title : label,
            OnClick = onClick,
        }, children.ToArray());
    }

    /// <summary>A section heading inside a dialog.</summary>
    public static Node SectionLabel(string text) =>
        Div(new() { ClassName = "field-label" }, Text(text));

    /// <summary>A subtle line of guidance.</summary>
    public static Node Hint(string text) =>
        Div(new() { ClassName = "panel-hint" }, Text(text));

    /// <summary>A row of labelled text and a value.</summary>
    public static Node Readout(string label, string value) =>
        Div(new() { ClassName = "readout-row" },
            Span(new() { ClassName = "readout-label" }, Text(label)),
            Span(new() { ClassName = "readout-value" }, Text(value)));
}
