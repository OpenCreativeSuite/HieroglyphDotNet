# Hieroglyph.NET

A typeface design application for the [Open Creative Suite](https://github.com/barely-sentient) — built for people who have never made a font before, without taking shortcuts that experienced designers will notice.

Draw a character, watch the alphabet come together, export a real font file, install it, and start typing with it. Everything stays on your machine.

## The workflow

1. **Create a font** — give it a family name, or start from the included *Occam* starter typeface and reshape it.
2. **Pick a character** from the browser on the left. Every letter, digit and mark is listed with a live miniature and a completion dot.
3. **Draw it** — drag with the **brush** for a freehand stroke that becomes a smooth, editable outline, place precise curves with the **pen**, or start from an ellipse, rectangle or another character.
4. **Shape it** — drag anchors and Bézier handles, nudge with the arrow keys, flip, centre, adjust the advance width and sidebearings.
5. **Preview** — the dock at the bottom renders real text in your developing typeface as you edit.
6. **Export** — validate, then write TTF, OTF, WOFF or WOFF2. Install straight into the system font collection on Windows.

## Features

- **Brush** — freehand paint that is simplified, smoothed and offset into one editable closed contour.
- **Pen and node editing** — cubic curves, mirrored handles, corner/smooth points, multi-node marquee selection.
- **Real metrics** — units per em, ascender, descender, cap height, x-height, advance width and sidebearings, drawn as guides on the canvas.
- **Kerning editor** — visual pair preview, suggested pairs, numeric fine-tuning; written to both a legacy `kern` table and a GPOS pair-positioning lookup.
- **Live preview dock** — sample text, alphabet and kerning tabs with size, tracking and leading controls, on paper or charcoal.
- **Specimen viewer** — a presentation sheet showing large glyphs, a paragraph, the full alphabet and the metrics.
- **Styles** — several weights/widths per family, each with its own outlines.
- **Project files** — a plain `.hgl` JSON document you own and can inspect.
- **Font compilation from scratch** — TrueType quadratic outlines, CFF Type 2 charstrings, cmap formats 4 and 12, name, OS/2, hmtx, post, `kern` and GPOS.

## Export formats

| Format | Outlines | Use |
| --- | --- | --- |
| TTF | TrueType (quadratic) | Installing on any computer |
| OTF | CFF (cubic) | Design software, print workflows |
| WOFF | TrueType, zlib | Older websites |
| WOFF2 | CFF, Brotli | Modern websites |

Blank characters are skipped rather than blocking the export; the validation list explains what is unfinished in plain language.

## Keyboard

| Key | Action |
| --- | --- |
| `V` / `A` | Selection / node editing |
| `P` | Pen |
| `B` | Brush |
| `H` / `Z` | Hand / zoom |
| `[` / `]` | Previous / next character |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+C` / `Ctrl+V` | Copy / paste a character |
| `Ctrl+D` | Copy a character to another code point |
| `Ctrl+E` | Export |
| `Ctrl+S` | Save the project |
| `Delete` | Delete selected points |
| `Escape` | End a stroke, then deselect |
| Arrows | Nudge 10 units (Shift: 1) |
| Wheel | Zoom around the pointer |

## Building

Requires the .NET 10 SDK.

```sh
dotnet run
```

The desktop host also accepts `--screenshot <path>` for a headless frame capture.

## Project layout

```
Program.cs                     application root and overlay stack
src/Core/                      documents, geometry, editing, export
  Glyph.cs, FontProject.cs     the data model
  FontEditor*.cs               session, tools, input, files, commands
  GlyphRasterizer.cs           antialiased scanline rasteriser
  StrokeOutline.cs             freehand brush → editable outline
  TrueTypeOutlines.cs          cubic → quadratic conversion
  CffTable.cs                  Type 2 charstrings and CFF container
  OpenTypeTables.cs            cmap, name, OS/2, hhea, head, maxp, post, kern, GPOS
  SfntBuilder.cs               sfnt, WOFF and WOFF2 containers
  DemoFont.cs                  the starter typeface
src/Components/                chrome, browser, canvas, inspector, preview, dialogs
src/Components/App/App.css     the entire visual identity
```

## Licence

Part of the Open Creative Suite. Data sovereignty: no accounts, no cloud, no telemetry.
