namespace HieroglyphDotNet.Core;

/// <summary>How serious a validation finding is.</summary>
public enum IssueLevel
{
    /// <summary>Useful information; nothing to fix.</summary>
    Info,

    /// <summary>The font will export, but something looks unfinished.</summary>
    Warning,

    /// <summary>Export cannot continue.</summary>
    Error,
}

/// <summary>One finding from the export check.</summary>
public sealed record FontIssue(IssueLevel Level, string Message, string? Detail = null);

/// <summary>Checks a style before it is compiled.</summary>
public static class FontValidator
{
    /// <summary>Validates the style and returns findings in display order.</summary>
    public static List<FontIssue> Validate(FontProject project, FontStyle style)
    {
        List<FontIssue> issues = [];

        if (string.IsNullOrWhiteSpace(project.FamilyName))
        {
            issues.Add(new FontIssue(IssueLevel.Error, "The font needs a family name.", "Set it in the Font panel on the right."));
        }

        if (style.Metrics.UnitsPerEm < 16 || style.Metrics.UnitsPerEm > 16384)
        {
            issues.Add(new FontIssue(IssueLevel.Error, "Units per em must be between 16 and 16384."));
        }

        List<Glyph> glyphs = style.Glyphs.Values.OrderBy(glyph => glyph.Codepoint).ToList();
        int drawn = glyphs.Count(glyph => glyph.HasOutline);
        int blank = glyphs.Count - drawn;

        if (drawn == 0)
        {
            issues.Add(new FontIssue(IssueLevel.Error, "There is nothing to export yet.", "Draw at least one character first."));
            return issues;
        }

        if (blank > 0)
        {
            List<Glyph> examples = glyphs.Where(glyph => !glyph.HasOutline && !CharacterSet.IsSpace(glyph.Codepoint)).Take(6).ToList();
            string list = string.Join(" ", examples.Select(glyph => glyph.Character).Where(character => character.Length > 0));
            issues.Add(new FontIssue(
                IssueLevel.Warning,
                blank == 1 ? "1 character is still blank." : $"{blank} characters are still blank.",
                list.Length > 0 ? $"For example: {list}. Blank characters are simply skipped in the font." : null));
        }

        List<Glyph> openContours = glyphs
            .Where(glyph => glyph.Contours.Any(contour => !contour.Closed && contour.Nodes.Count > 2))
            .Take(5)
            .ToList();

        if (openContours.Count > 0)
        {
            string list = string.Join(" ", openContours.Select(glyph => glyph.Character).Where(character => character.Length > 0));
            issues.Add(new FontIssue(
                IssueLevel.Warning,
                openContours.Count == 1 ? "1 character has an open contour." : $"{openContours.Count} characters have open contours.",
                $"Open contours are treated as closed when exported: {list}"));
        }

        List<Glyph> thin = glyphs.Where(glyph => glyph.HasOutline && glyph.NodeCount < 3).Take(5).ToList();
        if (thin.Count > 0)
        {
            issues.Add(new FontIssue(
                IssueLevel.Warning,
                "Some outlines have too few points to draw.",
                string.Join(" ", thin.Select(glyph => glyph.Character))));
        }

        List<Glyph> noAdvance = glyphs
            .Where(glyph => glyph.HasOutline && glyph.AdvanceWidth <= 0 && !CharacterSet.IsSpace(glyph.Codepoint))
            .Take(5)
            .ToList();

        if (noAdvance.Count > 0)
        {
            issues.Add(new FontIssue(
                IssueLevel.Warning,
                "Some characters have no advance width.",
                $"They would overlap the next letter: {string.Join(" ", noAdvance.Select(glyph => glyph.Character))}"));
        }

        List<Glyph> overflow = glyphs
            .Where(glyph => glyph.HasOutline && glyph.RightSideBearing < -1 && !CharacterSet.IsSpace(glyph.Codepoint))
            .Take(5)
            .ToList();

        if (overflow.Count > 0)
        {
            issues.Add(new FontIssue(
                IssueLevel.Warning,
                "Some outlines extend past their advance width.",
                $"They may touch the next letter: {string.Join(" ", overflow.Select(glyph => glyph.Character))}"));
        }

        if (style.Kerning.Count > 0)
        {
            issues.Add(new FontIssue(IssueLevel.Info, $"{style.Kerning.Count} kerning pairs will be included."));
        }

        if (issues.Count == 0)
        {
            issues.Add(new FontIssue(IssueLevel.Info, "Everything checks out. Ready to export."));
        }

        return issues;
    }

    /// <summary>Whether a set of findings allows export.</summary>
    public static bool CanExport(IEnumerable<FontIssue> issues) =>
        !issues.Any(issue => issue.Level == IssueLevel.Error);
}
