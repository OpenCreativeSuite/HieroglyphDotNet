using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>Lays out and paints text using the project's own outlines.</summary>
public static class Typeface
{
    /// <summary>The advance of one glyph in pixels.</summary>
    public static float Advance(FontStyle style, Glyph glyph, float scale, float tracking)
    {
        float extra = tracking * scale;
        return (glyph.AdvanceWidth * scale) + extra;
    }

    /// <summary>Measures a run of text in pixels.</summary>
    public static float Measure(FontStyle style, string text, float scale, float tracking)
    {
        float width = 0f;
        int previous = -1;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int code = rune.Value;
            Glyph? glyph = style.FindGlyph(code);
            if (glyph is null)
            {
                if (code == '\n')
                {
                    continue;
                }

                previous = -1;
                width += 300f * scale;
                continue;
            }

            if (previous >= 0)
            {
                width += style.Kern(previous, code) * scale;
            }

            width += Advance(style, glyph, scale, tracking);
            previous = code;
        }

        return width;
    }

    /// <summary>Paints one glyph with its origin at the pen position.</summary>
    public static void DrawGlyph(
        CanvasContext context,
        GlyphRasterCache cache,
        Glyph glyph,
        float scale,
        float penX,
        float baselineY,
        uint color)
    {
        if (!glyph.HasOutline || scale <= 0f)
        {
            return;
        }

        GlyphBitmap bitmap = cache.Get(glyph, scale);
        GlyphRasterizer.Paint(
            context,
            bitmap,
            MathF.Round(penX) - bitmap.OriginX,
            MathF.Round(baselineY) - bitmap.OriginY,
            color);
    }

    /// <summary>Paints a single line of text and returns the pen position after it.</summary>
    public static float DrawLine(
        CanvasContext context,
        GlyphRasterCache cache,
        FontStyle style,
        string text,
        float x,
        float baselineY,
        float scale,
        float tracking,
        uint color)
    {
        float pen = x;
        int previous = -1;

        foreach (Rune rune in text.EnumerateRunes())
        {
            int code = rune.Value;
            if (code == '\n' || code == '\r')
            {
                continue;
            }

            Glyph? glyph = style.FindGlyph(code);
            if (glyph is null)
            {
                pen += 300f * scale;
                previous = -1;
                continue;
            }

            if (previous >= 0)
            {
                pen += style.Kern(previous, code) * scale;
            }

            DrawGlyph(context, cache, glyph, scale, pen, baselineY, color);
            pen += Advance(style, glyph, scale, tracking);
            previous = code;
        }

        return pen;
    }

    /// <summary>Draws text centred horizontally on a point.</summary>
    public static void DrawCentred(
        CanvasContext context,
        GlyphRasterCache cache,
        FontStyle style,
        string text,
        float centreX,
        float baselineY,
        float scale,
        float tracking,
        uint color)
    {
        float width = Measure(style, text, scale, tracking);
        DrawLine(context, cache, style, text, centreX - (width * 0.5f), baselineY, scale, tracking, color);
    }

    /// <summary>Draws a glyph scaled to fit a box, centred on the em square.</summary>
    public static void DrawInBox(
        CanvasContext context,
        GlyphRasterCache cache,
        FontStyle style,
        Glyph glyph,
        Rect box,
        uint color,
        float fill = 0.72f)
    {
        if (!glyph.HasOutline)
        {
            return;
        }

        FontMetrics metrics = style.Metrics;
        float scale = MathF.Min(
            (box.Height * fill) / MathF.Max(1, metrics.UnitsPerEm),
            (box.Width * fill) / MathF.Max(1, glyph.AdvanceWidth));

        if (scale <= 0f)
        {
            return;
        }

        float baseline = box.Y + (box.Height * 0.5f) + ((metrics.CapHeight * scale) * 0.5f);
        float pen = box.X + ((box.Width - (glyph.AdvanceWidth * scale)) * 0.5f);
        DrawGlyph(context, cache, glyph, scale, pen, baseline, color);
    }

    /// <summary>Splits a paragraph into lines that fit a width.</summary>
    public static List<string> Wrap(
        FontStyle style,
        string text,
        float scale,
        float tracking,
        float maxWidth)
    {
        List<string> lines = [];
        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add("");
                continue;
            }

            string current = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && Measure(style, candidate, scale, tracking) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);
        }

        return lines;
    }
}
