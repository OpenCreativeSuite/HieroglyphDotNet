using System.Globalization;

namespace HieroglyphDotNet.Core;

/// <summary>The tools available above the glyph canvas.</summary>
public enum GlyphTool
{
    /// <summary>Move whole characters and marquee-select nodes.</summary>
    Select,

    /// <summary>Select and drag anchors and Bézier handles.</summary>
    Node,

    /// <summary>Place anchors and pull curve handles.</summary>
    Pen,

    /// <summary>Drag freely to paint a smooth filled stroke.</summary>
    Brush,

    /// <summary>Pan the canvas.</summary>
    Hand,

    /// <summary>Zoom in, or out with Alt.</summary>
    Zoom,
}

/// <summary>Which panel of the bottom preview dock is showing.</summary>
public enum PreviewTab
{
    /// <summary>Arbitrary sample text.</summary>
    Text,

    /// <summary>The full alphabet specimen.</summary>
    Alphabet,

    /// <summary>Common kerning pairs.</summary>
    Kerning,
}

/// <summary>The editing session behind every panel.</summary>
public sealed partial class FontEditor
{
    private static readonly float[] ZoomSteps =
    [
        0.05f, 0.075f, 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, 0.5f, 0.65f, 0.8f,
        1f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f, 8f,
    ];

    private sealed record Snapshot(int Code, Glyph? Glyph, Dictionary<(int Left, int Right), int>? Kerning, FontMetrics? Metrics);

    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private readonly GlyphRasterCache _raster = new();
    private readonly Settings _settings = Settings.Load();
    private int _selectedCode = 'A';
    private bool _viewInitialised;
    private int _geometryVersion;

    /// <summary>Creates a session with the starter typeface loaded.</summary>
    public FontEditor()
    {
        Project = DemoFont.Create();
        ShowWelcome = !_settings.WelcomeSeen;
        _lastFolder = _settings.LastFolder;
        NotifyStatus("Starter typeface loaded. Pick a letter and start shaping.");
    }

    /// <summary>Raised when the component tree should re-render.</summary>
    public Action? Refresh { get; set; }

    /// <summary>The project being edited.</summary>
    public FontProject Project { get; private set; }

    /// <summary>The style being edited.</summary>
    public FontStyle Style => Project.ActiveStyle;

    /// <summary>The raster cache shared by previews and thumbnails.</summary>
    public GlyphRasterCache Raster
    {
        get
        {
            _raster.Sync(_geometryVersion);
            return _raster;
        }
    }

    /// <summary>A counter bumped whenever geometry changes.</summary>
    public int GeometryVersion => _geometryVersion;

    /// <summary>The active tool.</summary>
    public GlyphTool Tool { get; private set; } = GlyphTool.Node;

    /// <summary>The code point being edited.</summary>
    public int SelectedCode => _selectedCode;

    /// <summary>The glyph being edited.</summary>
    public Glyph? SelectedGlyph => Style.FindGlyph(_selectedCode);

    /// <summary>The selected contour index, or -1.</summary>
    public int SelectedContour { get; private set; } = -1;

    /// <summary>The selected node indices within the selected contour.</summary>
    public SortedSet<int> SelectedNodes { get; } = [];

    /// <summary>The first selected node, or -1.</summary>
    public int SelectedNode => SelectedNodes.Count > 0 ? SelectedNodes.Min : -1;

    /// <summary>Current zoom, in canvas pixels per font unit.</summary>
    public float Zoom { get; private set; } = 0.6f;

    /// <summary>Canvas x of the glyph origin.</summary>
    public float PanX { get; private set; }

    /// <summary>Canvas y of the baseline.</summary>
    public float PanY { get; private set; }

    /// <summary>Canvas width in pixels.</summary>
    public float ViewWidth { get; private set; }

    /// <summary>Canvas height in pixels.</summary>
    public float ViewHeight { get; private set; }

    /// <summary>The current view transform.</summary>
    public GlyphView View => new(Zoom, PanX, PanY);

    /// <summary>The document cursor position in font units.</summary>
    public Vec2 CursorUnits { get; private set; }

    /// <summary>Whether the pointer is over the glyph canvas.</summary>
    public bool CursorOnCanvas { get; private set; }

    /// <summary>Whether the grid overlay is on.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Whether metric guides are on.</summary>
    public bool ShowMetrics { get; set; } = true;

    /// <summary>Whether sidebearing guides are on.</summary>
    public bool ShowBearings { get; set; } = true;

    /// <summary>Whether the character browser is on.</summary>
    public bool ShowBrowser { get; private set; } = true;

    /// <summary>Whether the inspector is on.</summary>
    public bool ShowInspector { get; private set; } = true;

    /// <summary>Whether the preview dock is on.</summary>
    public bool ShowPreview { get; private set; } = true;

    /// <summary>The active preview tab.</summary>
    public PreviewTab PreviewTab { get; private set; } = PreviewTab.Text;

    /// <summary>The text shown in the preview dock.</summary>
    public string PreviewText { get; set; } = "Handgloves";

    /// <summary>Preview size in pixels.</summary>
    public float PreviewSize { get; set; } = 64f;

    /// <summary>Extra letter spacing in thousandths of an em.</summary>
    public float PreviewTracking { get; set; }

    /// <summary>Line height multiplier.</summary>
    public float PreviewLeading { get; set; } = 1.35f;

    /// <summary>Whether the preview paints light text on a dark ground.</summary>
    public bool PreviewDark { get; set; }

    /// <summary>A short status line shown in the status bar.</summary>
    public string Status { get; private set; } = "Ready";

    /// <summary>Whether an empty glyph should offer starter shapes.</summary>
    public bool ShowStarterPanel { get; private set; } = true;

    /// <summary>Character search text in the browser.</summary>
    public string BrowserSearch { get; set; } = "";

    /// <summary>The browser category filter.</summary>
    public CharacterCategory? BrowserCategory { get; set; }

    /// <summary>Whether only characters that still need work are listed.</summary>
    public bool BrowserOnlyBlank { get; set; }

    /// <summary>The bitmap for a glyph at the current preview scale.</summary>
    public GlyphRasterCache RasterCache => Raster;

    /// <summary>Formats a number for display.</summary>
    public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Formats a number as an integer.</summary>
    public static string Whole(float value) => MathF.Round(value).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Parses a number entered by the user.</summary>
    public static float Parse(string text, float fallback)
    {
        string clean = text.Trim().Replace(" ", "").Replace(",", ".");
        return float.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    /// <summary>Parses a whole number entered by the user.</summary>
    public static int ParseInt(string text, int fallback) => (int)MathF.Round(Parse(text, fallback));

    /// <summary>Schedules a re-render.</summary>
    public void Notify()
    {
        Refresh?.Invoke();
    }

    /// <summary>Updates the status line and re-renders.</summary>
    public void NotifyStatus(string message)
    {
        Status = message;
        Notify();
    }

    /// <summary>Marks the project changed and bumps the geometry version.</summary>
    public void Touch()
    {
        Project.Dirty = true;
        _geometryVersion++;
        Notify();
    }

    /// <summary>Marks the project changed without invalidating rasterised glyphs.</summary>
    public void MarkDirty()
    {
        Project.Dirty = true;
        Notify();
    }

    // ------------------------------------------------------------- selection

    /// <summary>Selects a character, ending any pen stroke in progress.</summary>
    public void SelectGlyph(int codepoint)
    {
        if (codepoint == _selectedCode && Style.FindGlyph(codepoint) is not null)
        {
            return;
        }

        Project.EnsureCharacter(codepoint);
        FinishPen(close: false, silent: true);
        CancelBrush();
        _selectedCode = codepoint;
        ClearSelection();

        // The starter card is only helpful while a selection tool is active.
        // With the brush or pen in hand it would sit in the middle of the
        // canvas and swallow the first drag.
        ShowStarterPanel = Tool is GlyphTool.Select or GlyphTool.Node
            && Style.FindGlyph(codepoint)?.HasOutline != true;

        UpdateStatus();
        Notify();
    }

    /// <summary>Moves to the previous or next character in the browser order.</summary>
    public void StepGlyph(int direction)
    {
        List<Glyph> list = FilteredCharacters();
        if (list.Count == 0)
        {
            return;
        }

        int index = list.FindIndex(glyph => glyph.Codepoint == _selectedCode);
        if (index < 0)
        {
            index = 0;
        }

        index = (index + direction + list.Count) % list.Count;
        SelectGlyph(list[index].Codepoint);
    }

    /// <summary>The characters currently listed in the browser.</summary>
    public List<Glyph> FilteredCharacters()
    {
        IEnumerable<Glyph> query = Style.Glyphs.Values;

        if (BrowserCategory is { } category)
        {
            query = query.Where(glyph => CharacterSet.CategoryFor(glyph.Codepoint) == category);
        }

        if (BrowserOnlyBlank)
        {
            query = query.Where(glyph => !glyph.HasOutline);
        }

        string search = BrowserSearch.Trim();
        if (search.Length > 0)
        {
            query = query.Where(glyph =>
                glyph.Character.Contains(search, StringComparison.OrdinalIgnoreCase)
                || glyph.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || glyph.Codepoint.ToString("X4").Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(glyph => Array.IndexOf(CharacterSet.Order, CharacterSet.CategoryFor(glyph.Codepoint)))
            .ThenBy(glyph => glyph.Codepoint)
            .ToList();
    }

    /// <summary>Counts the characters in a category that have an outline.</summary>
    public (int Drawn, int Total) CategoryProgress(CharacterCategory category)
    {
        List<Glyph> list = Style.Glyphs.Values.Where(glyph => CharacterSet.CategoryFor(glyph.Codepoint) == category).ToList();
        return (list.Count(glyph => glyph.HasOutline), list.Count);
    }

    /// <summary>Clears the node selection.</summary>
    public void ClearSelection()
    {
        SelectedContour = -1;
        SelectedNodes.Clear();
    }

    /// <summary>Selects one node.</summary>
    public void SelectNode(int contour, int node, bool extend = false)
    {
        if (!extend || contour != SelectedContour)
        {
            SelectedContour = contour;
            SelectedNodes.Clear();
        }

        if (node >= 0)
        {
            SelectedNodes.Add(node);
        }

        Notify();
    }

    /// <summary>The bounding box of the selected nodes, or the whole outline.</summary>
    public Bounds SelectionBounds
    {
        get
        {
            Glyph? glyph = SelectedGlyph;
            if (glyph is null)
            {
                return Bounds.Zero;
            }

            if (SelectedContour >= 0 && SelectedContour < glyph.Contours.Count && SelectedNodes.Count > 0)
            {
                Bounds bounds = Bounds.Empty;
                GlyphContour contour = glyph.Contours[SelectedContour];
                foreach (int index in SelectedNodes)
                {
                    if (index >= 0 && index < contour.Nodes.Count)
                    {
                        bounds = bounds.With(contour.Nodes[index].Point);
                    }
                }

                return bounds;
            }

            return glyph.OutlineBounds;
        }
    }

    /// <summary>A description of the current selection for the status bar.</summary>
    public string SelectionLabel
    {
        get
        {
            Glyph? glyph = SelectedGlyph;
            if (glyph is null)
            {
                return "No character";
            }

            if (SelectedContour >= 0 && SelectedNodes.Count > 0)
            {
                GlyphContour? contour = SelectedContour < glyph.Contours.Count ? glyph.Contours[SelectedContour] : null;
                GlyphNode? node = contour is not null && SelectedNode >= 0 && SelectedNode < contour.Nodes.Count
                    ? contour.Nodes[SelectedNode]
                    : null;

                string kind = node is null ? "Node" : node.IsCurved ? (node.Smooth ? "Smooth node" : "Curve node") : "Corner node";
                string position = node is null ? "" : $"  {Number(node.Point.X)}, {Number(node.Point.Y)}";
                return $"{kind}{position}";
            }

            return glyph.HasOutline
                ? $"{glyph.Contours.Count} contour{(glyph.Contours.Count == 1 ? "" : "s")} · {glyph.NodeCount} points"
                : "Empty character";
        }
    }

    // ------------------------------------------------------------------ view

    /// <summary>Stores the canvas size and performs the first fit.</summary>
    public void SetViewSize(float width, float height)
    {
        ViewWidth = width;
        ViewHeight = height;

        if (!_viewInitialised && width > 8f && height > 8f)
        {
            _viewInitialised = true;
            FitGlyph(silent: true);
        }
    }

    /// <summary>Centres and scales the glyph so the em square fits comfortably.</summary>
    public void FitGlyph(bool silent = false)
    {
        if (ViewWidth < 8f || ViewHeight < 8f)
        {
            return;
        }

        Glyph? glyph = SelectedGlyph;
        int upm = Style.Metrics.UnitsPerEm;
        float advance = Math.Max(upm * 0.45f, glyph?.AdvanceWidth ?? upm * 0.6f);
        float top = Math.Max(Style.Metrics.Ascender, glyph?.OutlineBounds.MaxY ?? 0f) + 40f;
        float bottom = Math.Min(Style.Metrics.Descender, glyph?.OutlineBounds.MinY ?? 0f) - 40f;
        float height = Math.Max(240f, top - bottom);
        float width = Math.Max(advance + 160f, upm * 0.7f);

        float zoom = MathF.Min(ViewWidth / width, ViewHeight / height) * 0.88f;
        Zoom = Math.Clamp(zoom, 0.02f, 12f);
        PanX = (ViewWidth * 0.5f) - (advance * 0.5f * Zoom);
        PanY = (ViewHeight * 0.5f) + (((top + bottom) * 0.5f) * Zoom);

        if (!silent)
        {
            Notify();
        }
    }

    /// <summary>Zooms around a canvas point.</summary>
    public void ZoomAt(float factor, Vec2 screen)
    {
        float before = Zoom;
        Zoom = Math.Clamp(Zoom * factor, 0.02f, 16f);
        if (MathF.Abs(before - Zoom) < 1e-6f)
        {
            return;
        }

        PanX = screen.X - ((screen.X - PanX) * (Zoom / before));
        PanY = screen.Y - ((screen.Y - PanY) * (Zoom / before));
        Notify();
    }

    /// <summary>Steps to the next or previous zoom level around the canvas centre.</summary>
    public void ZoomStep(int direction)
    {
        Vec2 centre = new(ViewWidth * 0.5f, ViewHeight * 0.5f);
        float target = Zoom;
        if (direction > 0)
        {
            foreach (float step in ZoomSteps)
            {
                if (step > Zoom * 1.001f)
                {
                    target = step;
                    break;
                }
            }
        }
        else
        {
            for (int i = ZoomSteps.Length - 1; i >= 0; i--)
            {
                if (ZoomSteps[i] < Zoom * 0.999f)
                {
                    target = ZoomSteps[i];
                    break;
                }
            }
        }

        if (MathF.Abs(target - Zoom) < 1e-5f)
        {
            return;
        }

        ZoomAt(target / Zoom, centre);
    }

    /// <summary>Pans the view.</summary>
    public void PanBy(float dx, float dy)
    {
        PanX += dx;
        PanY += dy;
        Notify();
    }

    /// <summary>Points the canvas cursor at a font-unit position.</summary>
    public void SetCursor(Vec2 units, bool onCanvas)
    {
        CursorUnits = units;
        CursorOnCanvas = onCanvas;
        Notify();
    }

    // ----------------------------------------------------------------- tools

    /// <summary>Chooses a tool.</summary>
    public void SetTool(GlyphTool tool)
    {
        if (Tool == tool)
        {
            return;
        }

        if (Tool == GlyphTool.Pen)
        {
            FinishPen(close: false, silent: true);
        }

        if (Tool == GlyphTool.Brush)
        {
            CancelBrush();
        }

        Tool = tool;
        ShowStarterPanel = SelectedGlyph?.HasOutline != true && tool is GlyphTool.Select or GlyphTool.Node;
        UpdateStatus();
        Notify();
    }

    /// <summary>A readable name for a tool.</summary>
    public static string ToolName(GlyphTool tool) => tool switch
    {
        GlyphTool.Select => "Selection",
        GlyphTool.Node => "Node",
        GlyphTool.Pen => "Pen",
        GlyphTool.Brush => "Brush",
        GlyphTool.Hand => "Hand",
        _ => "Zoom",
    };

    /// <summary>The brush width in font units.</summary>
    public float BrushWidth { get; private set; } = 78f;

    /// <summary>Sets the brush width in font units.</summary>
    public void SetBrushWidth(float value)
    {
        BrushWidth = Math.Clamp(MathF.Round(value), 8f, 320f);
        NotifyStatus($"Brush size {BrushWidth:0} units");
    }

    /// <summary>Toggles the character browser.</summary>
    public void ToggleBrowser()
    {
        ShowBrowser = !ShowBrowser;
        Notify();
    }

    /// <summary>Toggles the inspector.</summary>
    public void ToggleInspector()
    {
        ShowInspector = !ShowInspector;
        Notify();
    }

    /// <summary>Toggles the preview dock.</summary>
    public void TogglePreview()
    {
        ShowPreview = !ShowPreview;
        Notify();
    }

    /// <summary>Switches the preview tab.</summary>
    public void SetPreviewTab(PreviewTab tab)
    {
        PreviewTab = tab;
        Notify();
    }

    /// <summary>Toggles the metric grid.</summary>
    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        Notify();
    }

    /// <summary>Toggles the spacing guides.</summary>
    public void ToggleMetrics()
    {
        ShowMetrics = !ShowMetrics;
        Notify();
    }

    /// <summary>Toggles the sidebearing guides.</summary>
    public void ToggleBearings()
    {
        ShowBearings = !ShowBearings;
        Notify();
    }

    /// <summary>Toggles the empty-character starter panel.</summary>
    public void ToggleStarterPanel()
    {
        ShowStarterPanel = !ShowStarterPanel;
        Notify();
    }

    // ----------------------------------------------------------------- undo

    /// <summary>Whether undo is available.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether redo is available.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Captures the current glyph, kerning and metrics before an edit.</summary>
    public void PushUndo()
    {
        Snapshot snapshot = new(
            _selectedCode,
            SelectedGlyph?.Clone(),
            new Dictionary<(int Left, int Right), int>(Style.Kerning),
            Style.Metrics.Clone());

        _undo.Add(snapshot);
        if (_undo.Count > 120)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>Captures state for an edit that only touches kerning.</summary>
    public void PushKerningUndo()
    {
        Snapshot snapshot = new(_selectedCode, null, new Dictionary<(int Left, int Right), int>(Style.Kerning), null);
        _undo.Add(snapshot);
        if (_undo.Count > 120)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>Reverts the last edit.</summary>
    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        Snapshot snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(Capture(snapshot.Code));
        Restore(snapshot);
        NotifyStatus("Undo");
    }

    /// <summary>Reapplies the last undone edit.</summary>
    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        Snapshot snapshot = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(Capture(snapshot.Code));
        Restore(snapshot);
        NotifyStatus("Redo");
    }

    private Snapshot Capture(int code) => new(
        _selectedCode,
        Style.FindGlyph(code)?.Clone(),
        new Dictionary<(int Left, int Right), int>(Style.Kerning),
        Style.Metrics.Clone());

    private void Restore(Snapshot snapshot)
    {
        if (snapshot.Glyph is not null)
        {
            Style.Glyphs[snapshot.Code] = snapshot.Glyph.Clone();
        }

        if (snapshot.Kerning is not null)
        {
            Style.Kerning.Clear();
            foreach (((int left, int right), int value) in snapshot.Kerning)
            {
                Style.Kerning[(left, right)] = value;
            }
        }

        if (snapshot.Metrics is not null)
        {
            Style.Metrics = snapshot.Metrics.Clone();
        }

        _selectedCode = snapshot.Code;
        ClearSelection();
        _geometryVersion++;
        Project.Dirty = true;
        UpdateStatus();
        Notify();
    }

    private void UpdateStatus()
    {
        Glyph? glyph = SelectedGlyph;
        Status = glyph is null
            ? "No character selected"
            : $"{glyph.Character}  {glyph.Name}  ·  {CharacterSet.CategoryFor(glyph.Codepoint)}";
    }
}
