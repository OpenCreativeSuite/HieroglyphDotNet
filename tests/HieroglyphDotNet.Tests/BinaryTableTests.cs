using System.Buffers.Binary;
using System.Text;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class ByteWriterTests
{
    [Fact]
    public void WriterEmitsBigEndianValuesAndTracksLength()
    {
        ByteWriter writer = new();
        writer.U8(0x01);
        writer.U16(0x0203);
        writer.I16(-2);
        writer.U32(0x04050607);
        writer.I32(-3);

        byte[] data = writer.ToArray();

        Assert.Equal(13, writer.Length);
        Assert.Equal(13, data.Length);
        Assert.Equal(0x01, data[0]);
        Assert.Equal(0x02, data[1]);
        Assert.Equal(0x03, data[2]);
        Assert.Equal(0xFF, data[3]);
        Assert.Equal(0xFE, data[4]);
        Assert.Equal([0x04, 0x05, 0x06, 0x07], data[5..9]);
        Assert.Equal(0xFF, data[9]);
        Assert.Equal(0xFD, data[12]);
    }

    [Fact]
    public void FixedPointAndTagsAreEncoded()
    {
        ByteWriter writer = new();
        writer.Fixed(1.5f);
        writer.Tag("cmap");
        writer.Tag("ab");
        writer.Tag("");

        byte[] data = writer.ToArray();

        Assert.Equal(0x00018000u, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal("cmap", Encoding.ASCII.GetString(data, 4, 4));
        Assert.Equal("ab  ", Encoding.ASCII.GetString(data, 8, 4));
        Assert.Equal("    ", Encoding.ASCII.GetString(data, 12, 4));
    }

    [Fact]
    public void AlignPatchAndBytesWork()
    {
        ByteWriter writer = new();
        writer.U8(0xAA);
        writer.Align4();
        Assert.Equal(4, writer.Length);

        writer.U32(0);
        writer.PatchU32(0, 0xDEADBEEF);
        writer.Bytes([0x01, 0x02]);

        byte[] data = writer.ToArray();
        Assert.Equal(0xDE, data[0]);
        Assert.Equal(0xEF, data[3]);
        Assert.Equal([0x01, 0x02], data[8..10]);
    }
}

public sealed class FontBinaryTests
{
    [Fact]
    public void ChecksumSumsBigEndianWordsAndPadsTheTail()
    {
        Assert.Equal(3u, FontBinary.Checksum([0, 0, 0, 1, 0, 0, 0, 2]));
        Assert.Equal(0u, FontBinary.Checksum([]));
        Assert.Equal(0xFF000000u, FontBinary.Checksum([0xFF]));
        Assert.Equal(0x01020300u, FontBinary.Checksum([1, 2, 3]));
    }

    [Fact]
    public void PadAlignsToFourBytesAndKeepsAlignedArrays()
    {
        byte[] aligned = [1, 2, 3, 4];
        Assert.Same(aligned, FontBinary.Pad(aligned));

        byte[] padded = FontBinary.Pad([1, 2, 3, 4, 5]);
        Assert.Equal(8, padded.Length);
        Assert.Equal([1, 2, 3, 4, 5, 0, 0, 0], padded);
    }

    [Fact]
    public void Base128EncodesVariableLengthIntegers()
    {
        static byte[] Encode(int value)
        {
            ByteWriter writer = new();
            FontBinary.Base128(writer, value);
            return writer.ToArray();
        }

        Assert.Equal([0x00], Encode(0));
        Assert.Equal([0x7F], Encode(127));
        Assert.Equal([0x81, 0x00], Encode(128));
        Assert.Equal([0x81, 0x80, 0x00], Encode(16384));
        Assert.Equal([0xFF, 0x7F], Encode(16383));
    }

    [Fact]
    public void DictAndCharStringIntegersUseTheExpectedEncodings()
    {
        ByteWriter dict = new();
        FontBinary.DictInt(dict, -123);
        Assert.Equal([29, 0xFF, 0xFF, 0xFF, 0x85], dict.ToArray());

        static byte[] Char(int value)
        {
            ByteWriter writer = new();
            FontBinary.CharStringInt(writer, value);
            return writer.ToArray();
        }

        Assert.Equal([139], Char(0));
        Assert.Equal([246], Char(107));
        Assert.Equal([247, 0], Char(108));
        Assert.Equal([32], Char(-107));
        Assert.Equal([251, 0], Char(-108));
        Assert.Equal([28, 0x13, 0x88], Char(5000));
        Assert.Equal([28, 0x80, 0x00], Char(-32768));
    }

    [Fact]
    public void TextHelpersConvertToMacRomanUtf16AndAscii()
    {
        Assert.Equal([(byte)'A', (byte)'?'], FontBinary.MacRoman("Aé"));
        Assert.Equal("A?", FontBinary.Ascii("Aé"));
        Assert.Equal([0x00, 0x41, 0x00, 0xE9], FontBinary.Utf16BigEndian("Aé"));
    }

    [Fact]
    public void OpenTypeDateCountsSecondsFromNineteenOhFour()
    {
        Assert.Equal(0, FontBinary.OpenTypeDate(new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(86400, FontBinary.OpenTypeDate(new DateTime(1904, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
    }
}

public sealed class SfntBuilderTests
{
    [Fact]
    public void SfntContainerSortsTablesAndWritesAValidDirectory()
    {
        SfntTable head = new("head", new byte[54]);
        SfntTable cmap = new("cmap", [1, 2, 3, 4, 5]);
        byte[] data = SfntBuilder.Build(0x00010000u, [head, cmap]);

        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4)));
        Assert.Equal("cmap", Encoding.ASCII.GetString(data, 12, 4));
        Assert.Equal("head", Encoding.ASCII.GetString(data, 28, 4));
        Assert.Equal(5u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + 12)));
        Assert.Equal(54u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(28 + 12)));

        // PatchU32 on head makes the whole-file checksum the magic constant.
        Assert.Equal(0xB1B0AFBAu, FontBinary.Checksum(data));
    }

    [Fact]
    public void SfntWithoutHeadStillBuilds()
    {
        byte[] data = SfntBuilder.Build(0x4F54544Fu, [new SfntTable("CFF ", [0, 1, 2, 3])]);

        Assert.Equal(0x4F54544Fu, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4)));
        Assert.Equal("CFF ", Encoding.ASCII.GetString(data, 12, 4));
    }

    [Fact]
    public void WoffWrapperHasTheRightHeaderAndDirectory()
    {
        SfntTable head = new("head", new byte[54]);
        byte[] data = SfntBuilder.BuildWoff(0x00010000u, [head]);

        Assert.Equal("wOFF"u8.ToArray(), data[..4]);
        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
        Assert.Equal((uint)data.Length, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12)));
        Assert.Equal(0, data.Length % 4);
    }

    [Fact]
    public void Woff2WrapperUsesBrotliAndHasTheRightLength()
    {
        SfntTable head = new("head", new byte[54]);
        byte[] data = SfntBuilder.BuildWoff2(0x00010000u, [head]);

        Assert.Equal("wOF2"u8.ToArray(), data[..4]);
        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
        Assert.Equal((uint)data.Length, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12)));
        Assert.Equal(0, data.Length % 4);
        Assert.True(data.Length > 48);
    }

    [Fact]
    public void TableChecksumMatchesTheSharedHelper()
    {
        SfntTable table = new("test", [1, 2, 3, 4]);

        Assert.Equal(FontBinary.Checksum([1, 2, 3, 4]), table.Checksum);
    }
}

public sealed class TrueTypeOutlineTests
{
    [Fact]
    public void BuildWritesAdvancesBearingsAndLocaOffsets()
    {
        Glyph notDef = Glyph.Blank(0, "notdef", 500);
        Glyph rect = Fx.DrawRect(Fx.Editor(), 'A', 100, 0, 500, 700, advance: 620).Clone();

        TrueTypeResult result = TrueTypeOutlines.Build([notDef, rect]);

        Assert.Equal(2, result.Advances.Length);
        Assert.Equal(500, result.Advances[0]);
        Assert.Equal(620, result.Advances[1]);
        Assert.Equal(0, result.LeftBearings[0]);
        Assert.Equal(100, result.LeftBearings[1]);
        Assert.Equal(new Bounds(100, 0, 500, 700), result.TotalBounds);
        Assert.Equal(100, result.XMin);
        Assert.Equal(0, result.YMin);
        Assert.Equal(700, result.YMax);
        Assert.NotEmpty(result.Glyf);
        Assert.Equal(12, result.Loca.Length);
        Assert.Equal((uint)result.Glyf.Length, BinaryPrimitives.ReadUInt32BigEndian(result.Loca.AsSpan(result.Loca.Length - 4)));
    }

    [Fact]
    public void BlankGlyphsProduceEmptyOutlines()
    {
        TrueTypeResult result = TrueTypeOutlines.Build([Glyph.Blank(0, "notdef", 600)]);

        Assert.Empty(result.Glyf);
        Assert.Equal(8, result.Loca.Length);
        Assert.True(result.TotalBounds.IsEmpty);
    }

    [Fact]
    public void QuadraticContourKeepsCornersAndConvertsCurves()
    {
        GlyphContour line = new();
        line.Nodes.Add(GlyphNode.Corner(new Vec2(0, 0)));
        line.Nodes.Add(GlyphNode.Corner(new Vec2(100, 0)));
        line.Nodes.Add(GlyphNode.Corner(new Vec2(0, 100)));
        line.Closed = true;

        List<TrueTypeOutlines.Point> linePoints = TrueTypeOutlines.QuadraticContour(line);
        Assert.Equal(3, linePoints.Count);
        Assert.All(linePoints, point => Assert.True(point.OnCurve));

        GlyphContour curve = new();
        curve.Nodes.Add(new GlyphNode
        {
            Point = new Vec2(0, 0),
            Out = new Vec2(50, 100),
            HasOut = true,
        });
        curve.Nodes.Add(new GlyphNode
        {
            Point = new Vec2(100, 0),
            In = new Vec2(50, 100),
            HasIn = true,
        });
        curve.Closed = true;

        List<TrueTypeOutlines.Point> curvePoints = TrueTypeOutlines.QuadraticContour(curve);
        Assert.True(curvePoints.Count > 2);
        Assert.Contains(curvePoints, point => !point.OnCurve);
        Assert.Equal(new Vec2(0, 0), curvePoints[0].Position);
    }
}

public sealed class CffTableTests
{
    [Fact]
    public void CffTableStartsWithAHeaderAndContainsTheFontName()
    {
        Glyph glyph = Fx.DrawRect(Fx.Editor(), 'A', 100, 0, 500, 700).Clone();
        FontMetrics metrics = new();

        byte[] data = CffTable.Build([Glyph.Blank(0, "notdef", 500), glyph], "TestFace-Regular", metrics);

        Assert.True(data.Length > 20);
        Assert.Equal(1, data[0]);
        Assert.Equal(0, data[1]);
        Assert.Equal(4, data[2]);
        Assert.Contains("TestFace-Regular", Encoding.ASCII.GetString(data));
    }

    [Fact]
    public void CffTableCanBuildOnlyANotdef()
    {
        byte[] data = CffTable.Build([Glyph.Blank(0, "notdef", 600)], "Empty-Regular", new FontMetrics());

        Assert.True(data.Length > 4);
        Assert.Equal(1, data[0]);
        Assert.Contains("Empty-Regular", Encoding.ASCII.GetString(data));
    }
}

public sealed class OpenTypeTableTests
{
    [Fact]
    public void CmapHasBmpAndFullUnicodeSubtables()
    {
        byte[] data = OpenTypeTables.Cmap([(0x41, 1), (0x42, 2), (0x1F600, 3)]);

        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(data));
        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2)));
        int format4 = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + 4));
        int format12 = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20 + 4));
        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(format4)));
        Assert.Equal(12, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(format12)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(format12 + 12)));
    }

    [Fact]
    public void CmapIgnoresInvalidMappingsAndSortsTheRest()
    {
        byte[] data = OpenTypeTables.Cmap([(0x42, 2), (0x41, 1), (0x43, 0), (-1, 1)]);

        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2)));
        int format4 = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + 4));
        Assert.Equal(0x41, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(format4 + 14)));
        Assert.Equal(0x42, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(format4 + 16)));
    }

    [Fact]
    public void NameTableContainsUtf16FamilyAndStyle()
    {
        FontProject project = FontProject.Create("Family Name");
        FontStyle style = project.ActiveStyle;
        byte[] data = OpenTypeTables.Name(project, style, "FamilyName-Regular");

        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(data));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2)));
        Assert.Contains("Family Name", Encoding.ASCII.GetString(data));
        Assert.Contains("Regular", Encoding.ASCII.GetString(data));
        Assert.Contains("FamilyName-Regular", Encoding.ASCII.GetString(data));
    }

    [Fact]
    public void Os2RecordsVersionWeightWidthAndKerningSelection()
    {
        FontStyle style = FontStyle.Create("Bold Italic", 700);
        style.Italic = true;
        byte[] data = OpenTypeTables.Os2(style, [600, 700], ['A', 'B'], hasKerning: true, 700, -200);

        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(data));
        Assert.Equal(700, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4)));
        Assert.Equal(5, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6)));
        Assert.Equal("HGLF", Encoding.ASCII.GetString(data, 58, 4));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(data.Length - 2)));
    }

    [Fact]
    public void Os2WithoutKerningUsesTheSimpleSelectionBit()
    {
        FontStyle style = FontStyle.Create("Regular", 400);
        byte[] data = OpenTypeTables.Os2(style, [600], ['A'], hasKerning: false, 700, -200);

        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(data.Length - 2)));
    }

    [Fact]
    public void HeadRecordsUnitsMagicAndBounds()
    {
        FontProject project = FontProject.Create("Head");
        project.Version = "2.500";
        FontMetrics metrics = project.ActiveStyle.Metrics;
        byte[] data = OpenTypeTables.Head(project, metrics, 10, -20, 600, 700, 1, 0);

        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal(0x00028000u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
        Assert.Equal(0x5F0F3CF5u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12)));
        Assert.Equal(1000, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(18)));
        Assert.Equal(10, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(36)));
        Assert.Equal(-20, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(38)));
        Assert.Equal(600, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(40)));
        Assert.Equal(700, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(42)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(44)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(50)));
        Assert.Equal(54, data.Length);
    }

    [Fact]
    public void HheaUsesTheLargestAdvanceAndTheItalicAngle()
    {
        FontStyle style = FontStyle.Create("Italic", 400);
        byte[] data = OpenTypeTables.Hhea(style, [500, 800], [10, 20], [-5, -8], [400, 500], 12f);

        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal(800, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(4)));
        Assert.Equal(-200, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(6)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(8)));
        Assert.Equal(800, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10)));
        Assert.Equal(10, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(12)));
        Assert.Equal(-8, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(14)));
        Assert.Equal(500, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(16)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(34)));
        Assert.Equal((short)MathF.Round(MathF.Tan(12f * MathF.PI / 180f) * 1000f), BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(20)));
    }

    [Fact]
    public void MaxpTablesRecordGlyphAndPointLimits()
    {
        byte[] truetype = OpenTypeTables.Maxp(42, 100, 5);
        byte[] cff = OpenTypeTables.MaxpCff(42);

        Assert.Equal(0x00010000u, BinaryPrimitives.ReadUInt32BigEndian(truetype));
        Assert.Equal(42, BinaryPrimitives.ReadUInt16BigEndian(truetype.AsSpan(4)));
        Assert.Equal(100, BinaryPrimitives.ReadUInt16BigEndian(truetype.AsSpan(6)));
        Assert.Equal(5, BinaryPrimitives.ReadUInt16BigEndian(truetype.AsSpan(8)));

        Assert.Equal(0x00005000u, BinaryPrimitives.ReadUInt32BigEndian(cff));
        Assert.Equal(42, BinaryPrimitives.ReadUInt16BigEndian(cff.AsSpan(4)));
    }

    [Fact]
    public void PostTableRecordsItalicAngleAndUnderline()
    {
        byte[] data = OpenTypeTables.Post(italic: true, italicAngle: -12.5f);

        Assert.Equal(0x00030000u, BinaryPrimitives.ReadUInt32BigEndian(data));
        Assert.Equal((int)MathF.Round(-12.5f * 65536f), BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4)));
        Assert.Equal(-100, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(8)));
        Assert.Equal(50, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(10)));
    }

    [Fact]
    public void KernTableRecordsSortedPairsAndGposIsNonEmpty()
    {
        List<(int Left, int Right, int Value)> pairs = [('V', 'A', -20), ('A', 'V', -30), ('A', 'W', 0)];
        byte[] kern = OpenTypeTables.Korn(pairs);
        byte[] gpos = OpenTypeTables.Gpos(pairs);

        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(kern));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(kern.AsSpan(2)));
        Assert.True(kern.Length > 20);
        Assert.NotEmpty(gpos);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(gpos));
    }
}
