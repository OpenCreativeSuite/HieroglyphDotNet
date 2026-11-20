using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

/// <summary>Shared fixtures for the font-engine and component tests.</summary>
internal static class Fx
{
    /// <summary>A blank project with a stable canvas view.</summary>
    public static FontEditor Editor()
    {
        FontEditor editor = new();
        editor.NewProject("TestFace", "Regular");
        editor.SetViewSize(1100f, 760f);
        editor.FitGlyph(silent: true);
        editor.ClearSelection();
        return editor;
    }

    /// <summary>The starter typeface with a stable canvas view.</summary>
    public static FontEditor Sample()
    {
        FontEditor editor = new();
        editor.LoadSample();
        editor.SetViewSize(1100f, 760f);
        editor.FitGlyph(silent: true);
        editor.ClearSelection();
        return editor;
    }

    /// <summary>Draws a rectangular contour into a glyph and selects it.</summary>
    public static Glyph DrawRect(
        FontEditor editor,
        int codepoint,
        float x0,
        float y0,
        float x1,
        float y1,
        int advance = 600)
    {
        editor.SelectGlyph(codepoint);
        Glyph glyph = editor.SelectedGlyph!;
        OutlineBuilder builder = new();
        builder.Rect(x0, y0, x1, y1);
        glyph.Contours = [.. builder.Contours];
        glyph.AdvanceWidth = advance;
        editor.Touch();
        return glyph;
    }

    /// <summary>Draws an elliptical ring into a glyph and selects it.</summary>
    public static Glyph DrawRing(FontEditor editor, int codepoint, float radius = 220f, float thickness = 60f)
    {
        editor.SelectGlyph(codepoint);
        Glyph glyph = editor.SelectedGlyph!;
        OutlineBuilder builder = new();
        builder.Ring(new Vec2(300, 300), radius, radius, thickness);
        glyph.Contours = [.. builder.Contours];
        editor.Touch();
        return glyph;
    }

    /// <summary>Converts font units to canvas pixels.</summary>
    public static Vec2 Screen(FontEditor editor, Vec2 units) => editor.View.ToScreen(units);

    /// <summary>Converts font units to canvas pixels.</summary>
    public static Vec2 Screen(FontEditor editor, float x, float y) => editor.View.ToScreen(x, y);

    /// <summary>Wraps a component in the application's provider.</summary>
    public static Node WithSession(FontEditor editor, Node child) =>
        Provider(SessionContext.Value, editor, child);

    /// <summary>Mounts a component against a supplied editor.</summary>
    public static ComponentTestHarness Render(FontEditor editor, Node child) =>
        ComponentTest.Render(WithSession(editor, child));

    /// <summary>Re-renders a mounted component after an editor mutation.</summary>
    public static void Reload<T>(ComponentTestHarness page, FontEditor editor)
        where T : Component, new() =>
        page.Update(WithSession(editor, C<T>()));

    /// <summary>Asserts a float is within a small tolerance.</summary>
    public static void Nearly(float expected, float actual, float tolerance = 0.05f) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);

    /// <summary>Builds a canvas-local event point from a canvas border box.</summary>
    public static Vec2 At(float x, float y) => new(x, y);

    /// <summary>Drags the pointer between two font-unit points.</summary>
    public static void Drag(FontEditor editor, Vec2 from, Vec2 to, bool shift = false, bool alt = false)
    {
        Vec2 start = editor.View.ToScreen(from);
        Vec2 end = editor.View.ToScreen(to);
        editor.PointerDown(start, shift, alt, middle: false);
        editor.PointerMove(end, shift);
        editor.PointerUp(end);
    }

    /// <summary>Clicks a font-unit point without moving the pointer.</summary>
    public static void Click(FontEditor editor, Vec2 point, bool shift = false, bool alt = false)
    {
        Vec2 screen = editor.View.ToScreen(point);
        editor.PointerDown(screen, shift, alt, middle: false);
        editor.PointerUp(screen);
    }

    /// <summary>Selects the first node of the first contour of a glyph.</summary>
    public static GlyphNode SelectFirstNode(FontEditor editor)
    {
        editor.SelectNode(0, 0);
        return editor.SelectedGlyph!.Contours[0].Nodes[0];
    }
}

/// <summary>Writes the host's modifier snapshot for gesture tests.</summary>
internal static class InputControl
{
    /// <summary>Sets a boolean modifier such as Shift, Alt, Middle or Control.</summary>
    public static void Set(string name, bool value) =>
        typeof(Donkeh.Windowing.InputState)
            .GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(null, [value]);

    /// <summary>Records the window-space pointer position.</summary>
    public static void Pointer(float x, float y) =>
        typeof(Donkeh.Windowing.InputState)
            .GetMethod("SetPointer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [x, y]);

    /// <summary>Sets the press counter used to discard stale pan gestures.</summary>
    public static void PressSequence(int value) =>
        typeof(Donkeh.Windowing.InputState)
            .GetProperty("PressSequence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(null, [value]);

    /// <summary>Clears every modifier after a test.</summary>
    public static void Reset()
    {
        Set("Shift", false);
        Set("Alt", false);
        Set("Control", false);
        Set("Middle", false);
    }
}

/// <summary>A temporary folder that is removed after the test.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HieroglyphDotNetTests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Cleanup must not hide the assertion result.
        }
    }
}
