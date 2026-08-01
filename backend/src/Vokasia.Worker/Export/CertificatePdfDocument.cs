using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vokasia.Worker.Export;

/// <summary>VOK-H5-E1 §5 — data identitas dipakai render PDF sertifikat (proyeksi minimal, tanpa entity EF).</summary>
public sealed record CertificateData(
    string StudentName, string SchoolName, string CompanyName, string PeriodLabel,
    DateOnly StartDate, DateOnly EndDate, decimal? FinalScore, string CertCode, string VerifyUrl);

/// <summary>
/// VOK-H5-E1 §5 — sertifikat PDF via QuestPDF (pre-approved). AC ticket minta "QR ke
/// /verify/{certCode}" - [CAKUPAN, GAP DISENGAJA dicatat eksplisit, BUKAN ditutupi diam-diam]:
/// QR CODE GAMBAR TIDAK dirender di sini. QRCoder (atau pustaka QR manapun) TIDAK ADA di
/// manapun di repo/PRD.md - beda dari QuestPDF yang literal pre-approved (PRD.md baris 82).
/// Menambahnya melanggar AGENTS.md rule #13 tanpa persetujuan Developer. Hand-roll ENCODER QR
/// dari nol (Reed-Solomon error correction dst.) BERBEDA dari MinimalXlsxWriter/kasus Xlsx:
/// benar-salahnya XML bisa dibuktikan lewat parse ulang (XDocument.Parse) TANPA alat luar - QR
/// code HANYA bisa benar2 diverifikasi lewat SCAN NYATA (kamera HP/QR reader), yang TIDAK bisa
/// dilakukan di sandbox ini - menulis encoder yang "kelihatannya benar" tapi tak pernah terbukti
/// scannable lebih berbahaya drpd tak ada sama sekali (sertifikat cetak dgn QR RUSAK tak bisa
/// ditarik ulang). Stopgap: URL verifikasi dicetak sbg TEKS BESAR yang mudah diketik ulang manual
/// (CertCode juga dicetak terpisah, alfanumerik, lihat CertCodeGenerator) - fungsi verifikasi
/// PUBLIK (VerifyCertificate endpoint) tetap ADA & bekerja, hanya cara aksesnya manual-ketik utk
/// sesi ini, bukan scan. Keputusan QR sungguhan (QRCoder vs hand-roll thd real device testing)
/// didokumentasikan DECISIONS.md D33, MINTA persetujuan Developer eksplisit.
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

                var qrSvg = QrCodeSvgGenerator.GenerateSvg(data.VerifyUrl, 100);
                col.Item().PaddingVertical(8).AlignCenter().Width(100).Height(100).Svg(qrSvg);

                col.Item().AlignCenter().Text($"Verifikasi keaslian di: {data.VerifyUrl}").FontSize(11);
            });

            page.Footer().AlignCenter().Text($"Diterbitkan otomatis oleh Vokasia — {DateOnly.FromDateTime(DateTime.UtcNow):d MMMM yyyy}").FontSize(9);
        });
    }
}
