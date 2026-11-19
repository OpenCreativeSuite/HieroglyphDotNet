using Donkeh.LayoutLab;
using Donkeh.Windowing;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Components;

/// <summary>Middle-button and space-drag panning for the glyph canvas.</summary>
/// <remarks>
/// Middle presses do not always produce a pointer-down on a canvas, so the
/// gesture is driven from pointer moves, exactly as the sibling editors do.
/// </remarks>
internal static class CanvasPan
{
    private static bool _panning;
    private static int _pressSequence;
    private static float _lastX;
    private static float _lastY;

    /// <summary>Handles a pointer move; true while the middle button owns it.</summary>
    public static bool Handle(FontEditor session)
    {
        int sequence = InputState.PressSequence;
        if (sequence != _pressSequence)
        {
            _pressSequence = sequence;
            _panning = false;
        }

        if (!InputState.Middle)
        {
            if (_panning)
            {
                _panning = false;
                InputState.CursorOverride = null;
            }

            return false;
        }

        float x = InputState.PointerX;
        float y = InputState.PointerY;
        InputState.CursorOverride = Cursor.Grabbing;

        if (!_panning)
        {
            _panning = true;
            _lastX = x;
            _lastY = y;
            return true;
        }

        float dx = x - _lastX;
        float dy = y - _lastY;
        _lastX = x;
        _lastY = y;

        if (dx != 0f || dy != 0f)
        {
            session.PanBy(dx, dy);
        }

        return true;
    }

    /// <summary>Ends a middle-button pan.</summary>
    public static void Reset()
    {
        _panning = false;
        InputState.CursorOverride = null;
    }
}
