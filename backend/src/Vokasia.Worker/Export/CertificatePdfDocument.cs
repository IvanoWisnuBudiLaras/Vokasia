using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vokasia.Worker.Export;

/// <summary>Minimal, non-EF projection used by the certificate renderer.</summary>
public sealed record CertificateData(
    string StudentName,
    string SchoolName,
    string CompanyName,
    string PeriodLabel,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? FinalScore,
    string CertCode,
    string VerifyUrl)
{
    public string MajorName { get; init; } = "-";
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Formal modern certificate: white dominant, neutral typography, restrained blue accent,
/// grayscale-safe hierarchy, and a QR that points to the canonical verification route.
/// </summary>
public sealed class CertificatePdfDocument(CertificateData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(38);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor(Colors.Grey.Darken3));

            page.Content().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(28).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(header =>
                    {
                        header.Item().Text(data.SchoolName.ToUpperInvariant()).FontSize(10).LetterSpacing(0.08f).Bold().FontColor(Colors.Grey.Medium);
                        header.Item().PaddingTop(9).Text("SERTIFIKAT PENYELESAIAN PKL").FontSize(25).Bold().FontColor(Colors.Black);
                        header.Item().PaddingTop(4).Text("Dokumen digital yang dapat diverifikasi secara publik").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.AutoItem().AlignTop().Text($"KODE\n{data.CertCode}").FontSize(9).Bold().AlignRight().FontColor(Colors.Grey.Darken1);
                });

                col.Item().PaddingTop(16).LineHorizontal(1).LineColor(Colors.Blue.Medium);
                col.Item().PaddingTop(25).Text("Diberikan kepada").FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().PaddingTop(4).Text(data.StudentName).FontSize(26).Bold().FontColor(Colors.Black);
                col.Item().PaddingTop(8).Text(data.MajorName).FontSize(12).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingTop(19).Text($"Telah menyelesaikan Praktik Kerja Lapangan di {data.CompanyName}.")
                    .FontSize(13).FontColor(Colors.Black);
                col.Item().PaddingTop(7).Text($"Periode: {data.PeriodLabel} ({data.StartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} – {data.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)})")
                    .FontSize(11).FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(25).Row(row =>
                {
                    row.AutoItem().Column(qrCol =>
                    {
                        var qrPng = QrCodeSvgGenerator.GeneratePng(data.VerifyUrl);
                        qrCol.Item().Width(84).Height(84).Image(qrPng);
                        qrCol.Item().PaddingTop(4).Text("Pindai untuk verifikasi").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    row.RelativeItem().PaddingLeft(18).AlignMiddle().Column(vCol =>
                    {
                        vCol.Item().Text("VERIFIKASI PUBLIK").FontSize(9).Bold().LetterSpacing(0.05f).FontColor(Colors.Grey.Medium);
                        vCol.Item().PaddingTop(5).Text(data.VerifyUrl).FontSize(10).FontColor(Colors.Black);
                        vCol.Item().PaddingTop(5).Text($"Diterbitkan: {data.IssuedAt.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("id-ID"))}").FontSize(10).FontColor(Colors.Grey.Darken2);
                    });
                });
            });

            page.Footer().AlignCenter().Text("Verifikasi status terbaru melalui tautan atau QR di atas · Vokasia").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
