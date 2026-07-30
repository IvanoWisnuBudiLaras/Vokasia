using System.IO.Compression;
using System.Xml.Linq;
using Vokasia.Worker.Export;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// VOK-H5-E1 §4 — membuktikan <see cref="MinimalXlsxWriter"/> menghasilkan .xlsx BENAR2 VALID
/// (bukan cuma "tidak crash saat generate"): dibuka ulang via ZipArchive BCL (Xlsx = zip OOXML),
/// setiap entry wajib well-formed XML (XDocument.Parse tak melempar), sel data yang ditulis harus
/// bisa ditemukan lagi lewat pembacaan sheet1.xml. Lihat doc-comment MinimalXlsxWriter ttg alasan
/// hand-rolled (gap ClosedXML, AGENTS.md #13).
/// </summary>
public class MinimalXlsxWriterTests
{
    [Fact]
    public void WriteSingleSheet_ProducesValidZipWithWellFormedXmlParts()
    {
        var headers = new[] { "Siswa", "Perusahaan", "Nilai Akhir" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Budi <Santoso>", "PT Aman & Sejahtera", 85.50m },
            new object?[] { "Siti \"Rahma\"", "CV Contoh", null },
        };

        var bytes = MinimalXlsxWriter.WriteSingleSheet(headers, rows);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var expectedEntries = new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml" };
        foreach (var entryName in expectedEntries)
        {
            var entry = archive.GetEntry(entryName);
            Assert.NotNull(entry);
            using var entryStream = entry!.Open();
            var doc = XDocument.Load(entryStream); // melempar kalau XML tak well-formed.
            Assert.NotNull(doc.Root);
        }
    }

    [Fact]
    public void WriteSingleSheet_HeaderAndDataCells_AreReadableBackFromSheetXml()
    {
        var headers = new[] { "Siswa", "Nilai" };
        var rows = new List<IReadOnlyList<object?>> { new object?[] { "Ahmad", 90.25m } };

        var bytes = MinimalXlsxWriter.WriteSingleSheet(headers, rows);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var sheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheetXml = new StreamReader(sheetStream).ReadToEnd();

        Assert.Contains("Siswa", sheetXml);
        Assert.Contains("Ahmad", sheetXml);
        Assert.Contains("90.25", sheetXml);
    }

    [Fact]
    public void WriteSingleSheet_SpecialXmlCharacters_AreEscapedNotBroken()
    {
        var headers = new[] { "Nama" };
        var rows = new List<IReadOnlyList<object?>> { new object?[] { "A & B <C> \"D\"" } };

        var bytes = MinimalXlsxWriter.WriteSingleSheet(headers, rows);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var sheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var doc = XDocument.Load(sheetStream); // AC: harus tetap well-formed walau data punya karakter spesial XML.

        var texts = doc.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value).ToList();
        Assert.Contains("A & B <C> \"D\"", texts);
    }
}
