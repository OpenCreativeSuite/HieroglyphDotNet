using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class FontEditorCommandTests
{
    [Fact]
    public void CopyAndPasteMoveAnIndependentOutline()
    {
        FontEditor editor = Fx.Editor();
        Glyph source = Fx.DrawRect(editor, 'A', 100, 0, 500, 700, advance: 620);

        editor.CopyGlyph();
        Assert.Contains("Copied A", editor.Status);

        editor.SelectGlyph('B');
        editor.PasteGlyph();

        Glyph target = editor.SelectedGlyph!;
        Assert.Equal(4, target.NodeCount);
        Assert.Equal(620, target.AdvanceWidth);
        Assert.NotSame(source.Contours[0], target.Contours[0]);

        target.Contours[0].Nodes[0].Point = Vec2.Zero;
        Assert.NotEqual(Vec2.Zero, source.Contours[0].Nodes[0].Point);
        Assert.Contains("Pasted A into B", editor.Status);
    }

    [Fact]
    public void PasteWithoutAClipboardExplainsWhatToDo()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        int nodes = glyph.NodeCount;

        editor.PasteGlyph();

        Assert.Equal(nodes, glyph.NodeCount);
        Assert.Contains("Nothing to paste", editor.Status);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void DuplicateToCopiesTheOutlineAndSelectsTheTarget()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700, advance: 600);
        editor.ShowDuplicateViaTest();

        editor.DuplicateTo('B');

        Assert.Equal('B', editor.SelectedCode);
        Assert.Equal(4, editor.SelectedGlyph!.NodeCount);
        Assert.Equal(600, editor.SelectedGlyph.AdvanceWidth);
        Assert.False(editor.ShowDuplicate);
        Assert.Contains("Copied A to B", editor.Status);
    }

    [Fact]
    public void DuplicateIgnoresTheSameTargetOrMissingSource()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.DuplicateTo('A');
        Assert.False(editor.CanUndo);

        editor.DuplicateTo('B');
        Assert.True(editor.CanUndo);
    }

    [Fact]
    public void ClearGlyphEmptiesTheOutlineAndRestoresTheStarterPanel()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.ToggleStarterPanel();
        Assert.False(editor.ShowStarterPanel);

        editor.ClearGlyph();

        Assert.False(glyph.HasOutline);
        Assert.True(editor.ShowStarterPanel);
        Assert.Contains("Cleared A", editor.Status);
    }

    [Fact]
    public void ClearGlyphIsANoOpForBlankCharacters()
    {
        FontEditor editor = Fx.Editor();

        editor.ClearGlyph();

        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void FlipHorizontalMirrorsAroundTheAdvanceCentre()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 300, 700, advance: 600);

        editor.FlipHorizontal();

        Assert.Equal(300f, glyph.OutlineBounds.MinX);
        Assert.Equal(500f, glyph.OutlineBounds.MaxX);
        Assert.Equal(200f, glyph.OutlineBounds.Width);
    }

    [Fact]
    public void FlipVerticalMirrorsAroundTheCapAndDescenderCentre()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 100, 500, 600);

        editor.FlipVertical();

        Assert.Equal(-100f, glyph.OutlineBounds.MinY);
        Assert.Equal(400f, glyph.OutlineBounds.MaxY);
    }

    [Fact]
    public void CentreGlyphEqualisesTheSidebearings()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 300, 700, advance: 600);

        editor.CentreGlyph();

        Assert.Equal(200f, glyph.OutlineBounds.MinX);
        Assert.Equal(400f, glyph.OutlineBounds.MaxX);
        Assert.Equal(200, glyph.LeftSideBearing);
        Assert.Equal(200, glyph.RightSideBearing);
    }

    [Fact]
    public void TransformCommandsDoNothingForBlankGlyphs()
    {
        FontEditor editor = Fx.Editor();

        editor.FlipHorizontal();
        editor.FlipVertical();
        editor.CentreGlyph();

        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void DeleteSelectionRemovesTheChosenNodesFromTheBack()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 1);
        editor.SelectNode(0, 3, extend: true);

        editor.DeleteSelection();

        Assert.Equal(2, glyph.Contours[0].Nodes.Count);
        Assert.Equal(new Vec2(100, 0), glyph.Contours[0].Nodes[0].Point);
        Assert.Equal(new Vec2(500, 700), glyph.Contours[0].Nodes[1].Point);
        Assert.Equal(-1, editor.SelectedContour);
    }

    [Fact]
    public void DeletingMostOfAContourRemovesTheContour()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 0);
        editor.SelectNode(0, 1, extend: true);
        editor.SelectNode(0, 2, extend: true);

        editor.DeleteSelection();

        Assert.Empty(glyph.Contours);
        Assert.True(editor.ShowStarterPanel);
    }

    [Fact]
    public void DeleteWithoutASelectionExplainsTheAlternatives()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.DeleteSelection();
        Assert.Contains("Select points", editor.Status);

        editor.SelectGlyph('B');
        editor.DeleteSelection();
        Assert.Contains("Nothing to delete", editor.Status);
    }

    [Fact]
    public void SelectAllNodesSelectsTheFirstContour()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.SelectAllNodes();

        Assert.Equal(0, editor.SelectedContour);
        Assert.Equal(4, editor.SelectedNodes.Count);
        Assert.Equal(0, editor.SelectedNode);
        Assert.Contains("all points", editor.Status);
    }

    [Fact]
    public void SelectAllOnABlankGlyphDoesNothing()
    {
        FontEditor editor = Fx.Editor();

        editor.SelectAllNodes();

        Assert.Empty(editor.SelectedNodes);
    }

    [Fact]
    public void ToggleNodeSmoothBuildsAndClearsHandles()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        GlyphNode node = Fx.SelectFirstNode(editor);
        editor.SelectedNodes.Clear();
        editor.SelectNode(0, 1);

        editor.ToggleNodeSmooth();

        GlyphNode smooth = editor.SelectedGlyph!.Contours[0].Nodes[1];
        Assert.True(smooth.Smooth);
        Assert.True(smooth.HasIn);
        Assert.True(smooth.HasOut);
        Assert.Equal("Smooth point", editor.Status);

        editor.ToggleNodeSmooth();
        Assert.False(smooth.Smooth);
        Assert.Equal("Corner point", editor.Status);
        Assert.NotEqual(node.Point, Vec2.Zero);
    }

    [Fact]
    public void ToggleNodeSmoothRequiresASelection()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.ToggleNodeSmooth();

        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void RetractHandleRemovesTheChosenHandle()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SelectNode(0, 1);
        editor.ToggleNodeSmooth();
        GlyphNode node = editor.SelectedGlyph!.Contours[0].Nodes[1];

        editor.RetractHandle(outgoing: true);
        Assert.False(node.HasOut);
        Assert.False(node.Smooth);
        Assert.Equal(node.Point, node.Out);

        editor.ToggleNodeSmooth();
        editor.RetractHandle(outgoing: false);
        Assert.False(node.HasIn);
        Assert.Equal(node.Point, node.In);
        Assert.Contains("retracted", editor.Status);
    }

    [Fact]
    public void StarterEllipseRectangleAndStemCreateOutlines()
    {
        FontEditor editor = Fx.Editor();

        editor.AddStarterEllipse();
        Glyph glyph = editor.SelectedGlyph!;
        Assert.Equal(2, glyph.Contours.Count);
        Assert.False(editor.ShowStarterPanel);

        editor.SelectGlyph('B');
        editor.AddStarterRectangle();
        Assert.Equal(2, editor.SelectedGlyph!.Contours.Count);

        editor.SelectGlyph('C');
        editor.AddStarterStem();
        Assert.Single(editor.SelectedGlyph!.Contours);
        Assert.Contains("stem", editor.Status);
    }

    [Fact]
    public void SetAdvanceClampsAndIgnoresNoOps()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.ClearUndoViaTest();

        editor.SetAdvance(glyph.AdvanceWidth);
        Assert.False(editor.CanUndo);

        editor.SetAdvance(5000);
        Assert.Equal(4000, glyph.AdvanceWidth);
        editor.SetAdvance(-10);
        Assert.Equal(0, glyph.AdvanceWidth);
        Assert.Contains("Advance width 0", editor.Status);
    }

    [Fact]
    public void SetAdvanceDoesNothingWithoutAGlyph()
    {
        FontEditor editor = Fx.Editor();
        editor.RemoveCharacter('A');
        editor.Style.Glyphs.Clear();

        editor.SetAdvance(500);

        Assert.Equal(-1, editor.SelectedContour);
    }

    [Fact]
    public void SetLeftBearingMovesTheOutline()
    {
        FontEditor editor = Fx.Editor();
        Glyph glyph = Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.SetLeftBearing(50);

        Assert.Equal(50, glyph.LeftSideBearing);
        Assert.Equal(450f, glyph.OutlineBounds.MaxX);

        editor.SetLeftBearing(200);
        Assert.Equal(200, glyph.LeftSideBearing);
        Assert.Contains("Left sidebearing 200", editor.Status);
    }

    [Fact]
    public void SetUnitsPerEmRescalesOutlinesAdvancesMetricsAndKerning()
    {
        FontEditor editor = Fx.Sample();
        Glyph glyph = editor.Style.FindGlyph('A')!;
        float left = glyph.OutlineBounds.MinX;
        int advance = glyph.AdvanceWidth;
        int kern = editor.Style.Kern('A', 'V');

        editor.SetUnitsPerEm(2000);

        Assert.Equal(2000, editor.Style.Metrics.UnitsPerEm);
        Assert.Equal(1600, editor.Style.Metrics.Ascender);
        Assert.Equal(-400, editor.Style.Metrics.Descender);
        Assert.Equal(1400, editor.Style.Metrics.CapHeight);
        Assert.Equal(1000, editor.Style.Metrics.XHeight);
        Assert.Equal(advance * 2, glyph.AdvanceWidth);
        Assert.Equal(kern * 2, editor.Style.Kern('A', 'V'));
        Fx.Nearly(left * 2, glyph.OutlineBounds.MinX, 0.5f);
        Assert.Contains("Units per em 2000", editor.Status);
    }

    [Fact]
    public void SetUnitsPerEmClampsAndIgnoresNoOps()
    {
        FontEditor editor = Fx.Editor();

        editor.SetUnitsPerEm(50);
        Assert.Equal(100, editor.Style.Metrics.UnitsPerEm);

        editor.ClearUndoViaTest();
        editor.SetUnitsPerEm(100);
        Assert.False(editor.CanUndo);

        editor.SetUnitsPerEm(9999);
        Assert.Equal(4000, editor.Style.Metrics.UnitsPerEm);
    }

    [Fact]
    public void SetMetricClampsEachVerticalMetric()
    {
        FontEditor editor = Fx.Editor();
        FontMetrics metrics = editor.Style.Metrics;

        editor.SetMetric("ascender", 99999);
        Assert.Equal(2000, metrics.Ascender);

        editor.SetMetric("descender", 100);
        Assert.Equal(-1, metrics.Descender);

        editor.SetMetric("cap", 0);
        Assert.Equal(1, metrics.CapHeight);

        editor.SetMetric("xheight", 0);
        Assert.Equal(1, metrics.XHeight);

        editor.SetMetric("linegap", 99999);
        Assert.Equal(1000, metrics.LineGap);

        editor.SetMetric("unknown", 123);
        Assert.Equal(1, metrics.XHeight);
        Assert.True(editor.Project.Dirty);
    }

    [Fact]
    public void KerningCommandsClampRemoveAndNudge()
    {
        FontEditor editor = Fx.Editor();

        editor.SetKern('A', 'V', -50);
        Assert.Equal(-50, editor.CurrentKern);
        Assert.Equal('A', editor.KerningLeft);
        Assert.Equal('V', editor.KerningRight);

        editor.NudgeKern(-10);
        Assert.Equal(-60, editor.CurrentKern);

        editor.NudgeKern(5000);
        Assert.Equal(1000, editor.CurrentKern);

        editor.SelectKernPair('T', 'o');
        editor.SetKern('T', 'o', -90);
        Assert.Equal(-90, editor.CurrentKern);

        editor.RemoveKern();
        Assert.Equal(0, editor.CurrentKern);
        Assert.Equal(0, editor.Style.Kern('T', 'o'));
        Assert.Contains("removed", editor.Status);
    }

    [Fact]
    public void KerningPairsAreOrderedAndSkipZeroValues()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Kerning[('V', 'A')] = -20;
        editor.Style.Kerning[('A', 'V')] = -30;
        editor.Style.Kerning[('A', 'W')] = 0;

        List<(int Left, int Right, int Value)> pairs = editor.KerningPairs();

        Assert.Equal([('A', 'V', -30), ('V', 'A', -20)], pairs);
    }

    [Fact]
    public void CharOfHandlesValidInvalidAndAstralCodepoints()
    {
        Assert.Equal("A", FontEditor.CharOf('A'));
        Assert.Equal("?", FontEditor.CharOf(-1));
        Assert.Equal("?", FontEditor.CharOf(0));
        Assert.Equal(char.ConvertFromUtf32(0x1F600), FontEditor.CharOf(0x1F600));
        Assert.Equal("?", FontEditor.CharOf(0x110000));
    }

    [Fact]
    public void AddCharacterAddsEveryStyleAndSelectsIt()
    {
        FontEditor editor = Fx.Editor();
        editor.AddStyle("Bold", 700, italic: false);
        editor.SetActiveStyle(0);

        editor.AddCharacter(0x2603);

        Assert.All(editor.Project.Styles, style => Assert.True(style.Glyphs.ContainsKey(0x2603)));
        Assert.Equal(0x2603, editor.SelectedCode);
        Assert.True(editor.ShowStarterPanel);
        Assert.False(editor.ShowAddCharacter);
        Assert.Contains("added", editor.Status);
    }

    [Fact]
    public void RemoveCharacterSelectsTheFirstRemainingCharacter()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.SetKern('A', 'V', -20);
        editor.SelectGlyph('A');

        editor.RemoveCharacter('A');

        Assert.False(editor.Style.Glyphs.ContainsKey('A'));
        Assert.NotEqual('A', editor.SelectedCode);
        Assert.Equal(0, editor.Style.Kern('A', 'V'));
        Assert.Contains("Character removed", editor.Status);
    }

    [Fact]
    public void RemovingTheLastCharacterIsRefused()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Glyphs.Clear();
        editor.Style.Glyphs['A'] = Glyph.Blank('A', "Capital A", 600);

        editor.RemoveCharacter('A');

        Assert.Single(editor.Style.Glyphs);
        Assert.DoesNotContain("removed", editor.Status);
    }

    [Fact]
    public void StylesCanBeAddedActivatedRenamedAndDeleted()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);

        editor.AddStyle("Bold", 950, italic: true);

        Assert.Equal(2, editor.Project.Styles.Count);
        Assert.Equal(1, editor.Project.ActiveStyleIndex);
        Assert.Equal("Bold", editor.Style.Name);
        Assert.Equal(900, editor.Style.Weight);
        Assert.True(editor.Style.Italic);
        Assert.True(editor.Style.Glyphs['A'].HasOutline);

        editor.SetActiveStyle(0);
        Assert.Equal("Regular", editor.Style.Name);
        editor.SetActiveStyle(0);
        editor.SetActiveStyle(9);
        Assert.Equal(0, editor.Project.ActiveStyleIndex);

        editor.RenameStyle("  Book  ");
        Assert.Equal("Book", editor.Style.Name);
        editor.RenameStyle("   ");
        Assert.Equal("Book", editor.Style.Name);

        editor.SetActiveStyle(1);
        editor.DeleteStyle();
        Assert.Single(editor.Project.Styles);
        editor.DeleteStyle();
        Assert.Single(editor.Project.Styles);
        Assert.Contains("at least one style", editor.Status);
    }

    [Fact]
    public void AddStyleUsesAFallbackName()
    {
        FontEditor editor = Fx.Editor();

        editor.AddStyle("   ", 400, italic: false);

        Assert.Equal("New Style", editor.Style.Name);
    }

    [Fact]
    public void SetFamilyFieldUpdatesEveryMetadataField()
    {
        FontEditor editor = Fx.Editor();

        editor.SetFamilyField("family", "  New Family  ");
        editor.SetFamilyField("version", " 3.000 ");
        editor.SetFamilyField("designer", " Ada ");
        editor.SetFamilyField("copyright", " (c) ");
        editor.SetFamilyField("description", " Desc ");
        editor.SetFamilyField("license", " OFL ");

        Assert.Equal("New Family", editor.Project.FamilyName);
        Assert.Equal("3.000", editor.Project.Version);
        Assert.Equal(" Ada ", editor.Project.Designer);
        Assert.Equal(" (c) ", editor.Project.Copyright);
        Assert.Equal(" Desc ", editor.Project.Description);
        Assert.Equal(" OFL ", editor.Project.License);
        Assert.True(editor.Project.Dirty);
    }

    [Fact]
    public void AlphabetSpecimenGroupsCapitalsLowercaseNumbersAndPunctuation()
    {
        FontEditor editor = Fx.Editor();

        string specimen = editor.AlphabetSpecimen();
        string[] lines = specimen.Split('\n');

        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", lines[0]);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", lines[1]);
        Assert.Equal("0123456789", lines[2]);
        Assert.Contains("A", specimen);
        Assert.DoesNotContain("  ", lines[3]);
        Assert.EndsWith(" ", specimen);
    }

    [Fact]
    public void NewProjectStartsBlankWithTheStandardRepertoire()
    {
        FontEditor editor = Fx.Sample();
        editor.PushUndo();

        editor.NewProject("Fresh", "Display");

        Assert.Equal("Fresh", editor.Project.FamilyName);
        Assert.Equal("Display", editor.Style.Name);
        Assert.Equal(CharacterSet.Standard.Count, editor.Style.Glyphs.Count);
        Assert.Equal(0, editor.Project.DrawnCount);
        Assert.Equal('A', editor.SelectedCode);
        Assert.True(editor.ShowStarterPanel);
        Assert.False(editor.ShowNewFont);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void NewProjectFallsBackToRegularAndUntitled()
    {
        FontEditor editor = Fx.Editor();

        editor.NewProject("   ", "  ");

        Assert.Equal("Untitled", editor.Project.FamilyName);
        Assert.Equal("Regular", editor.Style.Name);
        Assert.Contains("created", editor.Status);
    }

    [Fact]
    public void NewFromSampleRenamesTheStarterTypeface()
    {
        FontEditor editor = Fx.Editor();

        editor.NewFromSample("  My Face ", " Book ");

        Assert.Equal("My Face", editor.Project.FamilyName);
        Assert.Equal("Book", editor.Style.Name);
        Assert.True(editor.Project.DrawnCount > 60);
        Assert.Null(editor.Project.Path);
        Assert.True(editor.Project.Dirty);
        Assert.False(editor.ShowStarterPanel);
        Assert.Contains("starter outlines", editor.Status);

        editor.NewFromSample("", "");
        Assert.Equal("My Typeface", editor.Project.FamilyName);
        Assert.Equal("Regular", editor.Style.Name);
    }

    [Fact]
    public void LoadSampleReplacesTheProject()
    {
        FontEditor editor = Fx.Editor();

        editor.LoadSample();

        Assert.Equal("Occam", editor.Project.FamilyName);
        Assert.True(editor.Project.DrawnCount > 60);
        Assert.Equal('A', editor.SelectedCode);
        Assert.Contains("Starter typeface opened", editor.Status);
    }

    [Fact]
    public void CreateBlankUsesNewProjectAndReportsIt()
    {
        FontEditor editor = Fx.Editor();

        editor.CreateBlank("Blank", "Regular");

        Assert.Equal("Blank", editor.Project.FamilyName);
        Assert.Equal(0, editor.Project.DrawnCount);
        Assert.Contains("pick a character", editor.Status);
    }
}

file static class EditorTestExtensions
{
    /// <summary>Shows the duplicate dialog without requiring a drawn outline.</summary>
    public static void ShowDuplicateViaTest(this FontEditor editor)
    {
        typeof(FontEditor)
            .GetProperty(nameof(FontEditor.ShowDuplicate))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(editor, [true]);
    }

    /// <summary>Empties the undo history.</summary>
    public static void ClearUndoViaTest(this FontEditor editor)
    {
        var history = (System.Collections.IList)typeof(FontEditor)
            .GetField("_undo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(editor)!;
        history.Clear();
    }
}
