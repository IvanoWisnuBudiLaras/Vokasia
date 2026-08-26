using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vokasia.Api.Export;

public sealed record AtsCvData(
    string StudentName,
    string ContactLabel,
    string SchoolName,
    string MajorName,
    string CompanyName,
    string PeriodLabel,
    string DurationLabel,
    string? Description,
    IReadOnlyList<string> Competencies,
    string? CertificateCode,
    DateTimeOffset? CertificateIssuedAt,
    string? VerificationUrl,
    string PortfolioUrl);

/// <summary>
/// Small dependency-free PDF writer for the public ATS export. It emits a real PDF
/// with a standard Helvetica text object, so extracted text remains selectable/searchable.
/// The certificate itself remains rendered by QuestPDF in the Worker.
/// </summary>
public static class AtsCvPdfWriter
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Left = 56;
    private const double MaxTextWidth = 82;

    public static byte[] Write(AtsCvData data)
    {
        var content = new StringBuilder();
        var y = 785d;

        Text(content, data.StudentName, Left, y, 20, bold: true);
        y -= 20;
        Text(content, data.ContactLabel, Left, y, 9);
        y -= 28;

        Section(content, "PENDIDIKAN", ref y);
        Body(content, data.MajorName, ref y);
        Body(content, data.SchoolName, ref y);
        y -= 10;

        Section(content, "PENGALAMAN PKL", ref y);
        Body(content, data.CompanyName, ref y, bold: true);
        Body(content, $"{data.PeriodLabel} · {data.DurationLabel}", ref y);
        if (!string.IsNullOrWhiteSpace(data.Description)) Body(content, data.Description!, ref y);
        y -= 10;

        Section(content, "KOMPETENSI TERVERIFIKASI", ref y);
        if (data.Competencies.Count == 0)
        {
            Body(content, "Belum ada kompetensi terverifikasi.", ref y);
        }
        else
        {
            foreach (var competency in data.Competencies.Take(12)) Body(content, $"- {competency}", ref y);
        }
        y -= 10;

        Section(content, "SERTIFIKAT + LINK VERIFIKASI", ref y);
        if (data.CertificateCode is null || data.VerificationUrl is null)
        {
            Body(content, "Sertifikat belum tersedia.", ref y);
        }
        else
        {
            Body(content, $"Kode sertifikat: {data.CertificateCode}", ref y);
            if (data.CertificateIssuedAt.HasValue)
            {
                Body(content, $"Diterbitkan: {data.CertificateIssuedAt.Value.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("id-ID"))}", ref y);
            }
            Body(content, $"Link verifikasi: {data.VerificationUrl}", ref y);
        }

        y -= 20;
        Text(content, $"Portofolio publik: {data.PortfolioUrl}", Left, y, 9);

        return BuildPdf(content.ToString());
    }

    private static void Section(StringBuilder content, string value, ref double y)
    {
        y -= 4;
        Text(content, value, Left, y, 10, bold: true);
        y -= 19;
    }

    private static void Body(StringBuilder content, string value, ref double y, bool bold = false)
    {
        foreach (var line in Wrap(value))
        {
            Text(content, line, Left, y, 10, bold);
            y -= 15;
        }
    }

    private static IEnumerable<string> Wrap(string value)
    {
        var words = Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > MaxTextWidth)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static void Text(StringBuilder content, string value, double x, double y, int size, bool bold = false)
    {
        var font = bold ? "/F2" : "/F1";
        content.Append("BT ").Append(font).Append(' ').Append(size).Append(" Tf 0 0 0 rg 1 0 0 1 ")
            .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Tm (")
            .Append(Escape(Normalize(value))).AppendLine(") Tj ET");
    }

    private static byte[] BuildPdf(string stream)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
        };

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(withoutMarks, "[^\\u0009\\u0020-\\u007E]", "?");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));
}
