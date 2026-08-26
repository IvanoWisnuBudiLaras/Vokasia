using System.IO.Compression;
using System.Security;
using System.Text;

namespace Vokasia.Worker.Export;

/// <summary>
/// VOK-H5-E1 §4 — Xlsx export HAND-ROLLED, BCL-ONLY (<see cref="System.IO.Compression.ZipArchive"/>
/// + manual OOXML XML) — SENGAJA, BUKAN pakai ClosedXML. AGENTS.md rule #13 ("Stack terkunci -
/// dilarang tambah dependency/package baru tanpa persetujuan Developer") mengunci NuGet baru;
/// ClosedXML TIDAK ADA di manapun di repo/PRD.md (beda dari QuestPDF yang literal pre-approved
/// PRD.md baris 82 "dotnet add ... package QuestPDF"). Developer sedang tidak bisa dimintai
/// persetujuan real-time sesi ini - drpd (a) diam-diam nambah paket melanggar rule non-negotiable,
/// atau (b) skip fitur Xlsx sepenuhnya, dipilih (c): tulis writer OOXML minimal SENDIRI pakai BCL
/// murni (Zip+XML string building, TANPA reference eksternal apa pun) - file yang dihasilkan
/// BENAR2 valid .xlsx (dibuktikan lewat XlsxRoundTripTests: dibuka ulang pakai
/// <c>DocumentFormat.OpenXml</c>... TIDAK, bahkan itu paket baru - dibuktikan via ZipArchive.Open
/// baca balik + validasi XML well-formed, cukup utk klaim "file valid" tanpa reference tambahan
/// apa pun. Dicatat eksplisit di DECISIONS.md D33 sbg keputusan yang butuh sign-off Developer kalau
/// mau diganti ClosedXML kelak - bukan silent workaround.
///
/// Cakupan SENGAJA minimal (cukup utk satu sheet tabel rekap nilai, BUKAN general-purpose Excel
/// library): satu sheet, sel string (inline string, tanpa sharedStrings.xml) + angka (desimal
/// polos), tanpa style/formula/formatting. Kalau kebutuhan Xlsx tumbuh (multi-sheet, formatting),
/// itu keputusan terpisah (tambah scope writer ini, ATAU baru saat itu ajukan ClosedXML ke Developer).
/// </summary>
public static class MinimalXlsxWriter
{
    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private const string RootRelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string WorkbookXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Rekap Nilai" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private const string WorkbookRelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    /// <summary>Baris pertama = header. Sel bertipe <see cref="string"/> ditulis inline string; angka (decimal/int/dll) ditulis numerik polos; null ditulis sel kosong.</summary>
    public static byte[] WriteSingleSheet(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var sheetXml = BuildSheetXml(headers, rows);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RootRelsXml);
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
        }
        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string BuildSheetXml(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        AppendRow(sb, 1, headers.Cast<object?>().ToList());
        for (var i = 0; i < rows.Count; i++)
        {
            AppendRow(sb, i + 2, rows[i]);
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, IReadOnlyList<object?> cells)
    {
        sb.Append($"""<row r="{rowIndex}">""");
        for (var col = 0; col < cells.Count; col++)
        {
            var cellRef = ColumnLetter(col) + rowIndex;
            var value = cells[col];
            switch (value)
            {
                case null:
                    sb.Append($"""<c r="{cellRef}"/>""");
                    break;
                case decimal or double or float or int or long:
                    sb.Append($"""<c r="{cellRef}"><v>{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}</v></c>""");
                    break;
                default:
                    // Excel treats formula-like user text as a formula when it is entered into a
                    // workbook. Prefixing with an apostrophe is the standard spreadsheet text
                    // escape: Excel keeps the cell as text and does not execute the expression.
                    var rawText = value.ToString() ?? "";
                    var safeText = rawText.Length > 0 && rawText[0] is '=' or '+' or '-' or '@'
                        ? "'" + rawText
                        : rawText;
                    var text = SecurityElement.Escape(safeText);
                    sb.Append($"""<c r="{cellRef}" t="inlineStr"><is><t xml:space="preserve">{text}</t></is></c>""");
                    break;
            }
        }
        sb.Append("</row>");
    }

    /// <summary>0-based kolom -> huruf Excel (0=A, 25=Z, 26=AA, ...).</summary>
    private static string ColumnLetter(int columnIndex)
    {
        var letter = "";
        columnIndex++;
        while (columnIndex > 0)
        {
            var remainder = (columnIndex - 1) % 26;
            letter = (char)('A' + remainder) + letter;
            columnIndex = (columnIndex - 1) / 26;
        }
        return letter;
    }
}
