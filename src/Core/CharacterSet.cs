using System.Globalization;

namespace HieroglyphDotNet.Core;

/// <summary>The groups shown in the character browser.</summary>
public enum CharacterCategory
{
    /// <summary>Capital letters.</summary>
    Uppercase,

    /// <summary>Lowercase letters.</summary>
    Lowercase,

    /// <summary>Digits.</summary>
    Numbers,

    /// <summary>Punctuation and typographic marks.</summary>
    Punctuation,

    /// <summary>Currency, maths and common symbols.</summary>
    Symbols,

    /// <summary>Any other character the user added.</summary>
    Custom,
}

/// <summary>One entry in the standard character set.</summary>
public sealed record CharacterInfo(int Codepoint, string Name, CharacterCategory Category)
{
    /// <summary>The character itself.</summary>
    public string Character => Codepoint is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(Codepoint) : "";

    /// <summary>The U+ notation.</summary>
    public string Code => $"U+{Codepoint:X4}";
}

/// <summary>The character repertoire a new project starts with.</summary>
public static class CharacterSet
{
    /// <summary>Categories in browser order.</summary>
    public static readonly CharacterCategory[] Order =
    [
        CharacterCategory.Uppercase,
        CharacterCategory.Lowercase,
        CharacterCategory.Numbers,
        CharacterCategory.Punctuation,
        CharacterCategory.Symbols,
        CharacterCategory.Custom,
    ];

    private static readonly Dictionary<int, CharacterInfo> ByCode = Build();

    /// <summary>Every standard character, in browser order.</summary>
    public static IReadOnlyList<CharacterInfo> Standard { get; } = ByCode.Values
        .OrderBy(info => Array.IndexOf(Order, info.Category))
        .ThenBy(info => info.Codepoint)
        .ToList();

    /// <summary>Finds a standard entry by code point.</summary>
    public static CharacterInfo? Find(int codepoint) =>
        ByCode.TryGetValue(codepoint, out CharacterInfo? info) ? info : null;

    /// <summary>The display name of any code point.</summary>
    public static string NameFor(int codepoint) =>
        Find(codepoint)?.Name ?? "U+" + codepoint.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>The category for a code point, defaulting to custom.</summary>
    public static CharacterCategory CategoryFor(int codepoint) =>
        Find(codepoint)?.Category ?? CharacterCategory.Custom;

    /// <summary>A readable label for a category.</summary>
    public static string Label(CharacterCategory category) => category switch
    {
        CharacterCategory.Uppercase => "Uppercase",
        CharacterCategory.Lowercase => "Lowercase",
        CharacterCategory.Numbers => "Numbers",
        CharacterCategory.Punctuation => "Punctuation",
        CharacterCategory.Symbols => "Symbols",
        _ => "Custom",
    };

    /// <summary>The default advance width used for a code point.</summary>
    public static int DefaultAdvance(int codepoint) => codepoint switch
    {
        0x20 => 260,
        >= 0x30 and <= 0x39 => 600,
        >= 0x41 and <= 0x5A => 640,
        >= 0x61 and <= 0x7A => 560,
        >= 0x2E and <= 0x2F => 340,
        0x2C or 0x27 or 0x22 or 0x3A or 0x3B or 0x21 or 0x2D => 320,
        0x28 or 0x29 or 0x5B or 0x5D => 380,
        _ => 600,
    };

    /// <summary>Whether two code points are the same character.</summary>
    public static bool IsSpace(int codepoint) => codepoint == 0x20 || codepoint == 0xA0;

    private static Dictionary<int, CharacterInfo> Build()
    {
        Dictionary<int, CharacterInfo> map = [];

        void Add(int code, CharacterCategory category, string name) =>
            map[code] = new CharacterInfo(code, name, category);

        string[] capitals =
        [
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        ];

        for (int i = 0; i < capitals.Length; i++)
        {
            Add(0x41 + i, CharacterCategory.Uppercase, "Capital " + capitals[i]);
        }

        string[] lowercaseNames =
        [
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        ];

        for (int i = 0; i < lowercaseNames.Length; i++)
        {
            Add(0x61 + i, CharacterCategory.Lowercase, "Small " + lowercaseNames[i]);
        }

        string[] digitNames =
        [
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        ];

        for (int i = 0; i < digitNames.Length; i++)
        {
            Add(0x30 + i, CharacterCategory.Numbers, digitNames[i]);
        }

        Add(0x20, CharacterCategory.Punctuation, "Space");

        (int Code, string Name)[] punctuation =
        [
            (0x2E, "Full Stop"),
            (0x2C, "Comma"),
            (0x3A, "Colon"),
            (0x3B, "Semicolon"),
            (0x21, "Exclamation Mark"),
            (0x3F, "Question Mark"),
            (0x27, "Apostrophe"),
            (0x22, "Quotation Mark"),
            (0x2D, "Hyphen"),
            (0x2013, "En Dash"),
            (0x2014, "Em Dash"),
            (0x28, "Left Parenthesis"),
            (0x29, "Right Parenthesis"),
            (0x5B, "Left Bracket"),
            (0x5D, "Right Bracket"),
            (0x7B, "Left Brace"),
            (0x7D, "Right Brace"),
            (0x2F, "Solidus"),
            (0x5C, "Reverse Solidus"),
            (0x26, "Ampersand"),
            (0x40, "Commercial At"),
            (0x2A, "Asterisk"),
            (0x23, "Number Sign"),
            (0x25, "Percent Sign"),
            (0x2B, "Plus Sign"),
            (0x3D, "Equals Sign"),
            (0x3C, "Less Than"),
            (0x3E, "Greater Than"),
            (0x5F, "Underscore"),
            (0x7E, "Tilde"),
            (0x5E, "Circumflex"),
            (0x60, "Grave"),
            (0x7C, "Vertical Line"),
            (0x2026, "Ellipsis"),
            (0x2018, "Left Single Quote"),
            (0x2019, "Right Single Quote"),
            (0x201C, "Left Double Quote"),
            (0x201D, "Right Double Quote"),
            (0x00AB, "Left Guillemet"),
            (0x00BB, "Right Guillemet"),
            (0x2039, "Left Angle Quote"),
            (0x203A, "Right Angle Quote"),
            (0x00A1, "Inverted Exclamation"),
            (0x00BF, "Inverted Question"),
        ];

        foreach ((int code, string name) in punctuation)
        {
            Add(code, CharacterCategory.Punctuation, name);
        }

        (int Code, string Name)[] symbols =
        [
            (0x20AC, "Euro Sign"),
            (0x00A3, "Pound Sign"),
            (0x00A5, "Yen Sign"),
            (0x00A2, "Cent Sign"),
            (0x00A9, "Copyright Sign"),
            (0x00AE, "Registered Sign"),
            (0x2122, "Trade Mark Sign"),
            (0x00B0, "Degree Sign"),
            (0x00B1, "Plus-Minus Sign"),
            (0x00D7, "Multiplication Sign"),
            (0x00F7, "Division Sign"),
            (0x00B5, "Micro Sign"),
            (0x00A7, "Section Sign"),
            (0x00B6, "Pilcrow Sign"),
            (0x2020, "Dagger"),
            (0x2021, "Double Dagger"),
            (0x2022, "Bullet"),
            (0x00B7, "Middle Dot"),
            (0x2190, "Leftwards Arrow"),
            (0x2192, "Rightwards Arrow"),
            (0x2191, "Upwards Arrow"),
            (0x2193, "Downwards Arrow"),
            (0x2605, "Black Star"),
            (0x2665, "Black Heart"),
        ];

        foreach ((int code, string name) in symbols)
        {
            Add(code, CharacterCategory.Symbols, name);
        }

        return map;
    }
}
