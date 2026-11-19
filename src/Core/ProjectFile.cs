using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HieroglyphDotNet.Core;

/// <summary>Reads and writes the native project file.</summary>
public static class ProjectFile
{
    /// <summary>The file extension for project files.</summary>
    public const string Extension = ".hgl";

    /// <summary>The filter shown in the file browser.</summary>
    public static readonly string[] Extensions = [".hgl", ".json"];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialises the project.</summary>
    public static byte[] Save(FontProject project)
    {
        ProjectDto dto = new()
        {
            FamilyName = project.FamilyName,
            Version = project.Version,
            Designer = project.Designer,
            Copyright = project.Copyright,
            Description = project.Description,
            License = project.License,
            SampleText = project.SampleText,
            ActiveStyle = project.ActiveStyleIndex,
            Styles = project.Styles.Select(StyleDto.From).ToList(),
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }

    /// <summary>Writes the project to a path.</summary>
    public static void SaveTo(FontProject project, string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, Save(project));
        project.Path = path;
        project.Dirty = false;
    }

    /// <summary>Loads a project from a path.</summary>
    public static FontProject Load(string path)
    {
        ProjectDto? dto = JsonSerializer.Deserialize<ProjectDto>(File.ReadAllBytes(path), Options);
        if (dto is null || dto.Styles.Count == 0)
        {
            throw new InvalidDataException("This file is not a Hieroglyph project.");
        }

        FontProject project = new()
        {
            FamilyName = string.IsNullOrWhiteSpace(dto.FamilyName) ? "Untitled" : dto.FamilyName,
            Version = string.IsNullOrWhiteSpace(dto.Version) ? "1.000" : dto.Version,
            Designer = dto.Designer,
            Copyright = dto.Copyright,
            Description = dto.Description,
            License = dto.License,
            SampleText = string.IsNullOrWhiteSpace(dto.SampleText) ? "The quick brown fox jumps over the lazy dog" : dto.SampleText,
            ActiveStyleIndex = Math.Max(0, dto.ActiveStyle),
        };

        foreach (StyleDto styleDto in dto.Styles)
        {
            FontStyle style = new()
            {
                Name = string.IsNullOrWhiteSpace(styleDto.Name) ? "Regular" : styleDto.Name,
                Weight = Math.Clamp(styleDto.Weight, 100, 900),
                WidthClass = Math.Clamp(styleDto.WidthClass, 1, 9),
                Italic = styleDto.Italic,
                Metrics = new FontMetrics
                {
                    UnitsPerEm = styleDto.UnitsPerEm <= 0 ? 1000 : styleDto.UnitsPerEm,
                    Ascender = styleDto.Ascender,
                    Descender = styleDto.Descender,
                    CapHeight = styleDto.CapHeight,
                    XHeight = styleDto.XHeight,
                    LineGap = styleDto.LineGap,
                },
            };

            foreach (GlyphDto glyphDto in styleDto.Glyphs)
            {
                Glyph glyph = new()
                {
                    Codepoint = glyphDto.Code,
                    Name = string.IsNullOrWhiteSpace(glyphDto.Name) ? CharacterSet.NameFor(glyphDto.Code) : glyphDto.Name,
                    AdvanceWidth = glyphDto.Advance,
                };

                foreach (ContourDto contourDto in glyphDto.Contours)
                {
                    GlyphContour contour = new() { Closed = contourDto.Closed };
                    foreach (NodeDto nodeDto in contourDto.Nodes)
                    {
                        contour.Nodes.Add(new GlyphNode
                        {
                            Point = new Vec2(nodeDto.X, nodeDto.Y),
                            In = new Vec2(nodeDto.InX, nodeDto.InY),
                            Out = new Vec2(nodeDto.OutX, nodeDto.OutY),
                            HasIn = nodeDto.HasIn,
                            HasOut = nodeDto.HasOut,
                            Smooth = nodeDto.Smooth,
                        });
                    }

                    glyph.Contours.Add(contour);
                }

                style.Glyphs[glyph.Codepoint] = glyph;
            }

            foreach (KerningDto pair in styleDto.Kerning)
            {
                style.Kerning[(pair.Left, pair.Right)] = pair.Value;
            }

            project.Styles.Add(style);
        }

        project.ActiveStyleIndex = Math.Clamp(project.ActiveStyleIndex, 0, project.Styles.Count - 1);
        project.Path = path;
        project.Dirty = false;

        // Guarantee the standard repertoire even for hand-edited files.
        foreach (CharacterInfo info in CharacterSet.Standard)
        {
            project.EnsureCharacter(info.Codepoint);
        }

        return project;
    }

    private sealed class ProjectDto
    {
        public string Format { get; set; } = "hieroglyph-project";

        public int FormatVersion { get; set; } = 1;

        public string FamilyName { get; set; } = "Untitled";

        public string Version { get; set; } = "1.000";

        public string Designer { get; set; } = "";

        public string Copyright { get; set; } = "";

        public string Description { get; set; } = "";

        public string License { get; set; } = "";

        public string SampleText { get; set; } = "";

        public int ActiveStyle { get; set; }

        public List<StyleDto> Styles { get; set; } = [];
    }

    private sealed class StyleDto
    {
        public string Name { get; set; } = "Regular";

        public int Weight { get; set; } = 400;

        public int WidthClass { get; set; } = 5;

        public bool Italic { get; set; }

        public int UnitsPerEm { get; set; } = 1000;

        public int Ascender { get; set; } = 800;

        public int Descender { get; set; } = -200;

        public int CapHeight { get; set; } = 700;

        public int XHeight { get; set; } = 500;

        public int LineGap { get; set; }

        public List<GlyphDto> Glyphs { get; set; } = [];

        public List<KerningDto> Kerning { get; set; } = [];

        public static StyleDto From(FontStyle style) => new()
        {
            Name = style.Name,
            Weight = style.Weight,
            WidthClass = style.WidthClass,
            Italic = style.Italic,
            UnitsPerEm = style.Metrics.UnitsPerEm,
            Ascender = style.Metrics.Ascender,
            Descender = style.Metrics.Descender,
            CapHeight = style.Metrics.CapHeight,
            XHeight = style.Metrics.XHeight,
            LineGap = style.Metrics.LineGap,
            Glyphs = style.Glyphs.Values
                .OrderBy(glyph => glyph.Codepoint)
                .Select(GlyphDto.From)
                .ToList(),
            Kerning = style.Kerning
                .Where(pair => pair.Value != 0)
                .OrderBy(pair => pair.Key.Left)
                .ThenBy(pair => pair.Key.Right)
                .Select(pair => new KerningDto { Left = pair.Key.Left, Right = pair.Key.Right, Value = pair.Value })
                .ToList(),
        };
    }

    private sealed class GlyphDto
    {
        public int Code { get; set; }

        public string Name { get; set; } = "";

        public int Advance { get; set; } = 600;

        public List<ContourDto> Contours { get; set; } = [];

        public static GlyphDto From(Glyph glyph) => new()
        {
            Code = glyph.Codepoint,
            Name = glyph.Name,
            Advance = glyph.AdvanceWidth,
            Contours = glyph.Contours.Select(ContourDto.From).ToList(),
        };
    }

    private sealed class ContourDto
    {
        public bool Closed { get; set; } = true;

        public List<NodeDto> Nodes { get; set; } = [];

        public static ContourDto From(GlyphContour contour) => new()
        {
            Closed = contour.Closed,
            Nodes = contour.Nodes.Select(NodeDto.From).ToList(),
        };
    }

    private sealed class NodeDto
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float InX { get; set; }

        public float InY { get; set; }

        public float OutX { get; set; }

        public float OutY { get; set; }

        public bool HasIn { get; set; }

        public bool HasOut { get; set; }

        public bool Smooth { get; set; }

        public static NodeDto From(GlyphNode node) => new()
        {
            X = MathF.Round(node.Point.X, 2),
            Y = MathF.Round(node.Point.Y, 2),
            InX = MathF.Round(node.In.X, 2),
            InY = MathF.Round(node.In.Y, 2),
            OutX = MathF.Round(node.Out.X, 2),
            OutY = MathF.Round(node.Out.Y, 2),
            HasIn = node.HasIn,
            HasOut = node.HasOut,
            Smooth = node.Smooth,
        };
    }

    private sealed class KerningDto
    {
        public int Left { get; set; }

        public int Right { get; set; }

        public int Value { get; set; }
    }
}
