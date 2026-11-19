using System.Text;

namespace HieroglyphDotNet.Core;

/// <summary>Editing commands, font metadata, kerning and style management.</summary>
public sealed partial class FontEditor
{
    private Glyph? _clipboard;

    // ------------------------------------------------------------- commands

    /// <summary>Copies the current character's outline.</summary>
    public void CopyGlyph()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        _clipboard = glyph.Clone();
        NotifyStatus($"Copied {glyph.Character}  ·  {glyph.Name}");
    }

    /// <summary>Pastes the copied outline into the current character.</summary>
    public void PasteGlyph()
    {
        Glyph? glyph = SelectedGlyph;
        if (_clipboard is null || glyph is null)
        {
            NotifyStatus("Nothing to paste yet. Copy a character first.");
            return;
        }

        PushUndo();
        glyph.Contours = _clipboard.Contours.Select(contour => contour.Clone()).ToList();
        glyph.AdvanceWidth = _clipboard.AdvanceWidth;
        ClearSelection();
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = !glyph.HasOutline;
        NotifyStatus($"Pasted {_clipboard.Character} into {glyph.Character}");
    }

    /// <summary>Copies the current outline to another character.</summary>
    public void DuplicateTo(int target)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || target == glyph.Codepoint)
        {
            return;
        }

        Project.EnsureCharacter(target);
        Glyph? destination = Style.FindGlyph(target);
        if (destination is null)
        {
            return;
        }

        PushUndo();
        destination.Contours = glyph.Contours.Select(contour => contour.Clone()).ToList();
        destination.AdvanceWidth = glyph.AdvanceWidth;
        _geometryVersion++;
        Project.Dirty = true;
        ShowDuplicate = false;
        _selectedCode = target;
        ClearSelection();
        ShowStarterPanel = false;
        NotifyStatus($"Copied {glyph.Character} to {destination.Character}");
    }

    /// <summary>Deletes every contour in the current character.</summary>
    public void ClearGlyph()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || !glyph.HasOutline)
        {
            return;
        }

        PushUndo();
        glyph.Contours.Clear();
        ClearSelection();
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = true;
        NotifyStatus($"Cleared {glyph.Character}");
    }

    /// <summary>Mirrors the current character horizontally around its advance centre.</summary>
    public void FlipHorizontal()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || !glyph.HasOutline)
        {
            return;
        }

        PushUndo();
        glyph.FlipHorizontal(glyph.AdvanceWidth * 0.5f);
        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus($"Flipped {glyph.Character} horizontally");
    }

    /// <summary>Mirrors the current character vertically around the x-height centre.</summary>
    public void FlipVertical()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || !glyph.HasOutline)
        {
            return;
        }

        PushUndo();
        glyph.FlipVertical((Style.Metrics.CapHeight + Style.Metrics.Descender) * 0.5f);
        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus($"Flipped {glyph.Character} vertically");
    }

    /// <summary>Centres the outline between the sidebearings.</summary>
    public void CentreGlyph()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || !glyph.HasOutline)
        {
            return;
        }

        PushUndo();
        Bounds bounds = glyph.OutlineBounds;
        float target = (glyph.AdvanceWidth - bounds.Width) * 0.5f;
        glyph.Translate(new Vec2(target - bounds.MinX, 0f));
        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus($"Centred {glyph.Character}");
    }

    /// <summary>Deletes the selected nodes, or the whole outline when nothing is selected.</summary>
    public void DeleteSelection()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        if (SelectedContour < 0 || SelectedNodes.Count == 0 || SelectedContour >= glyph.Contours.Count)
        {
            if (glyph.HasOutline)
            {
                ClearGlyph();
            }

            return;
        }

        PushUndo();
        GlyphContour contour = glyph.Contours[SelectedContour];
        foreach (int index in SelectedNodes.OrderByDescending(index => index))
        {
            if (index >= 0 && index < contour.Nodes.Count)
            {
                contour.Nodes.RemoveAt(index);
            }
        }

        if (contour.Nodes.Count < 2)
        {
            glyph.Contours.RemoveAt(SelectedContour);
        }

        ClearSelection();
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = !glyph.HasOutline;
        NotifyStatus("Deleted selected points");
    }

    /// <summary>Selects every node in the character.</summary>
    public void SelectAllNodes()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || glyph.Contours.Count == 0)
        {
            return;
        }

        SelectedNodes.Clear();
        SelectedContour = 0;
        for (int i = 0; i < glyph.Contours[0].Nodes.Count; i++)
        {
            SelectedNodes.Add(i);
        }

        NotifyStatus("Selected all points in the first contour");
    }

    /// <summary>Switches the selected node between corner and smooth.</summary>
    public void ToggleNodeSmooth()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || SelectedContour < 0 || SelectedContour >= glyph.Contours.Count || SelectedNodes.Count == 0)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[SelectedContour];
        if (SelectedNode < 0 || SelectedNode >= contour.Nodes.Count)
        {
            return;
        }

        PushUndo();
        GlyphNode node = contour.Nodes[SelectedNode];
        node.Smooth = !node.Smooth;

        if (node.Smooth)
        {
            // Give a corner a pair of handles along its neighbours so it can
            // be curved immediately.
            int index = SelectedNode;
            Vec2 previous = contour.Nodes[(index - 1 + contour.Nodes.Count) % contour.Nodes.Count].Point;
            Vec2 next = contour.Nodes[(index + 1) % contour.Nodes.Count].Point;
            Vec2 direction = (next - previous).Normalized();
            float length = MathF.Max(40f, (next - previous).Length * 0.25f);
            node.In = node.Point - (direction * length);
            node.Out = node.Point + (direction * length);
            node.HasIn = true;
            node.HasOut = true;
        }

        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus(node.Smooth ? "Smooth point" : "Corner point");
    }

    /// <summary>Removes one handle from the selected node.</summary>
    public void RetractHandle(bool outgoing)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || SelectedContour < 0 || SelectedContour >= glyph.Contours.Count || SelectedNodes.Count == 0)
        {
            return;
        }

        GlyphContour contour = glyph.Contours[SelectedContour];
        if (SelectedNode < 0 || SelectedNode >= contour.Nodes.Count)
        {
            return;
        }

        PushUndo();
        GlyphNode node = contour.Nodes[SelectedNode];
        if (outgoing)
        {
            node.HasOut = false;
            node.Out = node.Point;
            node.Smooth = false;
        }
        else
        {
            node.HasIn = false;
            node.In = node.Point;
            node.Smooth = false;
        }

        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus("Handle retracted");
    }

    /// <summary>Adds a starter ellipse ring to an empty character.</summary>
    public void AddStarterEllipse()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        PushUndo();
        FontMetrics metrics = Style.Metrics;
        float advance = MathF.Max(300f, glyph.AdvanceWidth);
        float thickness = metrics.UnitsPerEm * 0.078f;
        float radiusX = (advance * 0.5f) - 45f - (thickness * 0.5f);
        float radiusY = (metrics.XHeight * 0.5f) - (thickness * 0.5f);
        Vec2 centre = new(advance * 0.5f, metrics.XHeight * 0.5f);

        OutlineBuilder builder = new();
        builder.Ring(centre, radiusX, radiusY, thickness);
        glyph.Contours = [.. builder.Contours];
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = false;
        NotifyStatus("Added an ellipse — drag its points to shape it");
    }

    /// <summary>Adds a rectangular ring to an empty character.</summary>
    public void AddStarterRectangle()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        PushUndo();
        FontMetrics metrics = Style.Metrics;
        float advance = MathF.Max(300f, glyph.AdvanceWidth);
        float thickness = metrics.UnitsPerEm * 0.078f;
        float left = 45f;
        float right = advance - 45f;
        float bottom = 0f;
        float top = metrics.CapHeight;

        OutlineBuilder builder = new();
        builder.Polygon(
        [
            new Vec2(left, bottom),
            new Vec2(right, bottom),
            new Vec2(right, top),
            new Vec2(left, top),
        ]);
        builder.Polygon(
        [
            new Vec2(left + thickness, bottom + thickness),
            new Vec2(left + thickness, top - thickness),
            new Vec2(right - thickness, top - thickness),
            new Vec2(right - thickness, bottom + thickness),
        ], hole: true);

        glyph.Contours = [.. builder.Contours];
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = false;
        NotifyStatus("Added a rectangle — drag its points to shape it");
    }

    /// <summary>Adds a vertical stem to an empty character.</summary>
    public void AddStarterStem()
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        PushUndo();
        FontMetrics metrics = Style.Metrics;
        float thickness = metrics.UnitsPerEm * 0.078f;
        float centre = glyph.AdvanceWidth * 0.5f;

        OutlineBuilder builder = new();
        builder.VStem(centre, metrics.Descender, metrics.CapHeight, thickness);
        glyph.Contours = [.. builder.Contours];
        _geometryVersion++;
        Project.Dirty = true;
        ShowStarterPanel = false;
        NotifyStatus("Added a stem — perfect for I, l or the first stroke of a letter");
    }

    // ------------------------------------------------------------- metrics

    /// <summary>Changes the advance width of the current character.</summary>
    public void SetAdvance(int value)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null)
        {
            return;
        }

        int clamped = Math.Clamp(value, 0, 4000);
        if (clamped == glyph.AdvanceWidth)
        {
            return;
        }

        PushUndo();
        glyph.AdvanceWidth = clamped;
        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus($"Advance width {clamped}");
    }

    /// <summary>Moves the outline so it starts at a given sidebearing.</summary>
    public void SetLeftBearing(int value)
    {
        Glyph? glyph = SelectedGlyph;
        if (glyph is null || !glyph.HasOutline)
        {
            return;
        }

        PushUndo();
        glyph.Translate(new Vec2(value - glyph.LeftSideBearing, 0f));
        _geometryVersion++;
        Project.Dirty = true;
        NotifyStatus($"Left sidebearing {value}");
    }

    /// <summary>Changes the em size and rescales every outline to match.</summary>
    public void SetUnitsPerEm(int value)
    {
        int clamped = Math.Clamp(value, 100, 4000);
        FontMetrics metrics = Style.Metrics;
        if (clamped == metrics.UnitsPerEm)
        {
            return;
        }

        PushUndo();
        float factor = clamped / (float)metrics.UnitsPerEm;
        foreach (Glyph glyph in Style.Glyphs.Values)
        {
            glyph.Scale(Vec2.Zero, factor, factor);
            glyph.AdvanceWidth = (int)MathF.Round(glyph.AdvanceWidth * factor);
        }

        foreach ((int left, int right) in Style.Kerning.Keys.ToList())
        {
            Style.Kerning[(left, right)] = (int)MathF.Round(Style.Kerning[(left, right)] * factor);
        }

        metrics.UnitsPerEm = clamped;
        metrics.Ascender = (int)MathF.Round(metrics.Ascender * factor);
        metrics.Descender = (int)MathF.Round(metrics.Descender * factor);
        metrics.CapHeight = (int)MathF.Round(metrics.CapHeight * factor);
        metrics.XHeight = (int)MathF.Round(metrics.XHeight * factor);
        metrics.LineGap = (int)MathF.Round(metrics.LineGap * factor);

        _geometryVersion++;
        Project.Dirty = true;
        FitGlyph(silent: true);
        NotifyStatus($"Units per em {clamped}");
    }

    /// <summary>Updates a vertical metric.</summary>
    public void SetMetric(string name, int value)
    {
        FontMetrics metrics = Style.Metrics;
        switch (name)
        {
            case "ascender":
                metrics.Ascender = Math.Clamp(value, 1, metrics.UnitsPerEm * 2);
                break;
            case "descender":
                metrics.Descender = Math.Clamp(value, -metrics.UnitsPerEm * 2, -1);
                break;
            case "cap":
                metrics.CapHeight = Math.Clamp(value, 1, metrics.UnitsPerEm * 2);
                break;
            case "xheight":
                metrics.XHeight = Math.Clamp(value, 1, metrics.UnitsPerEm * 2);
                break;
            case "linegap":
                metrics.LineGap = Math.Clamp(value, 0, metrics.UnitsPerEm);
                break;
        }

        Project.Dirty = true;
        Notify();
    }

    // ------------------------------------------------------------- kerning

    /// <summary>The first character of the pair being previewed.</summary>
    public int KerningLeft { get; private set; } = 'A';

    /// <summary>The second character of the pair being previewed.</summary>
    public int KerningRight { get; private set; } = 'V';

    /// <summary>Common pairs offered as starting points.</summary>
    public static readonly (int Left, int Right)[] CommonPairs =
    [
        ('A', 'V'), ('V', 'A'), ('W', 'A'), ('A', 'W'), ('T', 'o'), ('T', 'a'),
        ('Y', 'o'), ('L', 'T'), ('P', '.'), ('F', ','), ('V', '.'), ('r', '.'),
    ];

    /// <summary>Every pair in the current style, ordered by left then right.</summary>
    public List<(int Left, int Right, int Value)> KerningPairs() =>
        Style.Kerning
            .Where(pair => pair.Value != 0)
            .OrderBy(pair => pair.Key.Left)
            .ThenBy(pair => pair.Key.Right)
            .Select(pair => (pair.Key.Left, pair.Key.Right, pair.Value))
            .ToList();

    /// <summary>The current value of the previewed pair.</summary>
    public int CurrentKern => Style.Kern(KerningLeft, KerningRight);

    /// <summary>Chooses a pair for the kerning preview.</summary>
    public void SelectKernPair(int left, int right)
    {
        KerningLeft = left;
        KerningRight = right;
        Notify();
    }

    /// <summary>Sets the kerning value of the previewed pair.</summary>
    public void SetKern(int left, int right, int value)
    {
        int clamped = Math.Clamp(value, -1000, 1000);
        PushKerningUndo();
        if (clamped == 0)
        {
            Style.Kerning.Remove((left, right));
        }
        else
        {
            Style.Kerning[(left, right)] = clamped;
        }

        KerningLeft = left;
        KerningRight = right;
        Project.Dirty = true;
        NotifyStatus(clamped == 0 ? "Kerning pair removed" : $"Kerning {CharOf(left)}{CharOf(right)}  {clamped}");
    }

    /// <summary>Adjusts the previewed pair by a step.</summary>
    public void NudgeKern(int delta) => SetKern(KerningLeft, KerningRight, CurrentKern + delta);

    /// <summary>Removes the previewed pair.</summary>
    public void RemoveKern() => SetKern(KerningLeft, KerningRight, 0);

    /// <summary>The character for a code point, or a placeholder.</summary>
    public static string CharOf(int codepoint) =>
        codepoint is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(codepoint) : "?";

    // -------------------------------------------------------------- project

    /// <summary>Creates a new empty project.</summary>
    public void NewProject(string familyName, string styleName)
    {
        FontProject project = FontProject.Create(familyName);
        project.ActiveStyle.Name = string.IsNullOrWhiteSpace(styleName) ? "Regular" : styleName.Trim();
        Project = project;
        _selectedCode = 'A';
        _undo.Clear();
        _redo.Clear();
        ClearSelection();
        _raster.Clear();
        _geometryVersion++;
        DismissWelcome();
        ShowNewFont = false;
        ShowStarterPanel = true;
        _viewInitialised = false;
        FitGlyph(silent: true);
        NotifyStatus($"{project.FamilyName} {project.ActiveStyle.Name} created");
    }

    /// <summary>Replaces the project with the starter typeface under a new name.</summary>
    public void NewFromSample(string familyName, string styleName)
    {
        FontProject project = DemoFont.Create();
        project.FamilyName = string.IsNullOrWhiteSpace(familyName) ? "My Typeface" : familyName.Trim();
        project.ActiveStyle.Name = string.IsNullOrWhiteSpace(styleName) ? "Regular" : styleName.Trim();
        project.Path = null;
        project.Dirty = true;
        Project = project;
        _selectedCode = 'A';
        _undo.Clear();
        _redo.Clear();
        ClearSelection();
        _raster.Clear();
        _geometryVersion++;
        DismissWelcome();
        ShowNewFont = false;
        ShowStarterPanel = false;
        _viewInitialised = false;
        FitGlyph(silent: true);
        NotifyStatus($"{project.FamilyName} {project.ActiveStyle.Name} created from the starter outlines");
    }

    /// <summary>Replaces the project with the starter typeface.</summary>
    public void LoadSample()
    {
        Project = DemoFont.Create();
        _selectedCode = 'A';
        _undo.Clear();
        _redo.Clear();
        ClearSelection();
        _raster.Clear();
        _geometryVersion++;
        DismissWelcome();
        ShowStarterPanel = false;
        _viewInitialised = false;
        FitGlyph(silent: true);
        NotifyStatus("Starter typeface opened");
    }

    /// <summary>Creates an empty project with the full standard character set.</summary>
    public void CreateBlank(string familyName, string styleName)
    {
        NewProject(familyName, styleName);
        NotifyStatus("New font created — pick a character and start drawing");
    }

    /// <summary>Adds a character to the project.</summary>
    public void AddCharacter(int codepoint)
    {
        Project.EnsureCharacter(codepoint);
        _geometryVersion++;
        Project.Dirty = true;
        _selectedCode = codepoint;
        ClearSelection();
        ShowAddCharacter = false;
        ShowStarterPanel = true;
        NotifyStatus($"{CharOf(codepoint)} added to the font");
    }

    /// <summary>Removes a character from the project.</summary>
    public void RemoveCharacter(int codepoint)
    {
        if (Style.Glyphs.Count <= 1)
        {
            return;
        }

        Project.RemoveCharacter(codepoint);
        if (_selectedCode == codepoint)
        {
            _selectedCode = FilteredCharacters().FirstOrDefault()?.Codepoint ?? 'A';
        }

        _geometryVersion++;
        Project.Dirty = true;
        ClearSelection();
        NotifyStatus("Character removed");
    }

    // --------------------------------------------------------------- styles

    /// <summary>Switches the active style.</summary>
    public void SetActiveStyle(int index)
    {
        if (index < 0 || index >= Project.Styles.Count || index == Project.ActiveStyleIndex)
        {
            return;
        }

        FinishPen(close: false, silent: true);
        Project.ActiveStyleIndex = index;
        _undo.Clear();
        _redo.Clear();
        ClearSelection();
        _raster.Clear();
        _geometryVersion++;
        if (Style.FindGlyph(_selectedCode) is null)
        {
            _selectedCode = 'A';
        }

        NotifyStatus($"Editing {Style.Name}");
    }

    /// <summary>Copies the current style into a new one.</summary>
    public void AddStyle(string name, int weight, bool italic)
    {
        string final = string.IsNullOrWhiteSpace(name) ? "New Style" : name.Trim();
        FontStyle clone = Style.Clone(final);
        clone.Weight = Math.Clamp(weight, 100, 900);
        clone.Italic = italic;
        Project.Styles.Add(clone);
        Project.ActiveStyleIndex = Project.Styles.Count - 1;
        Project.Dirty = true;
        _undo.Clear();
        _redo.Clear();
        ClearSelection();
        _geometryVersion++;
        NotifyStatus($"{final} added as a copy of the previous style");
    }

    /// <summary>Renames the active style.</summary>
    public void RenameStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Style.Name = name.Trim();
        Project.Dirty = true;
        Notify();
    }

    /// <summary>Deletes the active style.</summary>
    public void DeleteStyle()
    {
        if (Project.Styles.Count <= 1)
        {
            NotifyStatus("A family needs at least one style");
            return;
        }

        Project.Styles.RemoveAt(Project.ActiveStyleIndex);
        Project.ActiveStyleIndex = Math.Clamp(Project.ActiveStyleIndex, 0, Project.Styles.Count - 1);
        Project.Dirty = true;
        _undo.Clear();
        _redo.Clear();
        _geometryVersion++;
        NotifyStatus("Style deleted");
    }

    /// <summary>Sets the family-level metadata field.</summary>
    public void SetFamilyField(string field, string value)
    {
        switch (field)
        {
            case "family":
                Project.FamilyName = value.Trim();
                break;
            case "version":
                Project.Version = value.Trim();
                break;
            case "designer":
                Project.Designer = value;
                break;
            case "copyright":
                Project.Copyright = value;
                break;
            case "description":
                Project.Description = value;
                break;
            case "license":
                Project.License = value;
                break;
        }

        Project.Dirty = true;
        Notify();
    }

    /// <summary>A printable specimen string of every drawn character.</summary>
    public string AlphabetSpecimen()
    {
        StringBuilder builder = new();
        foreach (CharacterCategory category in new[] { CharacterCategory.Uppercase, CharacterCategory.Lowercase })
        {
            foreach (Glyph glyph in Style.Glyphs.Values
                         .Where(glyph => CharacterSet.CategoryFor(glyph.Codepoint) == category)
                         .OrderBy(glyph => glyph.Codepoint))
            {
                builder.Append(glyph.Character);
            }

            builder.Append('\n');
        }

        foreach (Glyph glyph in Style.Glyphs.Values
                     .Where(glyph => CharacterSet.CategoryFor(glyph.Codepoint) == CharacterCategory.Numbers)
                     .OrderBy(glyph => glyph.Codepoint))
        {
            builder.Append(glyph.Character);
        }

        builder.Append("\n");

        foreach (Glyph glyph in Style.Glyphs.Values
                     .Where(glyph => CharacterSet.CategoryFor(glyph.Codepoint) == CharacterCategory.Punctuation && !CharacterSet.IsSpace(glyph.Codepoint))
                     .OrderBy(glyph => glyph.Codepoint))
        {
            builder.Append(glyph.Character);
            builder.Append(' ');
        }

        return builder.ToString();
    }
}
