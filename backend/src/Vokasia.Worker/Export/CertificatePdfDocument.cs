using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vokasia.Worker.Export;

/// <summary>VOK-H5-E1 §5 — data identitas dipakai render PDF sertifikat (proyeksi minimal, tanpa entity EF).</summary>
public sealed record CertificateData(
    string StudentName, string SchoolName, string CompanyName, string PeriodLabel,
    DateOnly StartDate, DateOnly EndDate, decimal? FinalScore, string CertCode, string VerifyUrl);

/// <summary>
/// VOK-H5-E1 §5 — sertifikat PDF via QuestPDF dengan QR standar QRCoder.
/// Runtime device scanning remains a release verification gate.
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
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(12));

            page.Content().Column(col =>
            {
                col.Item().AlignCenter().Text("SERTIFIKAT PRAKTIK KERJA LAPANGAN").FontSize(24).Bold();
                col.Item().AlignCenter().Text(data.SchoolName).FontSize(16);
                col.Item().PaddingVertical(16);

                col.Item().AlignCenter().Text("Diberikan kepada:").FontSize(12);
                col.Item().AlignCenter().Text(data.StudentName).FontSize(20).Bold();
                col.Item().PaddingVertical(8);

                col.Item().AlignCenter().Text(
                    $"Telah menyelesaikan Praktik Kerja Lapangan di {data.CompanyName} " +
                    $"pada {data.PeriodLabel} ({data.StartDate:d MMMM yyyy} - {data.EndDate:d MMMM yyyy})" +
                    (data.FinalScore.HasValue ? $" dengan nilai akhir {data.FinalScore:0.00}." : "."));

                col.Item().PaddingVertical(16);
                col.Item().AlignCenter().Text($"Kode Verifikasi: {data.CertCode}").FontSize(14).Bold();

                var qrPng = QrCodeSvgGenerator.GeneratePng(data.VerifyUrl);
                col.Item().PaddingVertical(8).AlignCenter().Width(100).Height(100).Image(qrPng);

                col.Item().AlignCenter().Text($"Verifikasi keaslian di: {data.VerifyUrl}").FontSize(11);
            });

            page.Footer().AlignCenter().Text($"Diterbitkan otomatis oleh Vokasia — {DateOnly.FromDateTime(DateTime.UtcNow):d MMMM yyyy}").FontSize(9);
        });
    }
}
