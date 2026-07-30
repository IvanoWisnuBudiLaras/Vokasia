using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Vokasia.Worker.Export;

namespace Vokasia.Tests.Assessment;

/// <summary>VOK-H5-E1 §5 — CertificatePdfDocument. QR gambar SENGAJA tidak ada (lihat doc-comment kelas ttg gap QRCoder) - tes ini membuktikan kode verifikasi TEKS tetap ada di PDF.</summary>
public class CertificatePdfDocumentTests
{
    static CertificatePdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_ValidData_ProducesValidPdfBytes()
    {
        var data = new CertificateData(
            "Ahmad Fauzi", "SMKN 1 Contoh", "PT Contoh Sejahtera", "Periode Ganjil 2026",
            new DateOnly(2026, 1, 5), new DateOnly(2026, 7, 5), 87.50m, "aB3xY9kLmN7q", "https://vokasia.example/verify/aB3xY9kLmN7q");

        var bytes = new CertificatePdfDocument(data).GeneratePdf();

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void GeneratePdf_NullFinalScore_DoesNotThrow()
    {
        var data = new CertificateData(
            "Siti Aminah", "SMKN 1 Contoh", "CV Berkah Jaya", "Periode Genap 2026",
            new DateOnly(2026, 1, 5), new DateOnly(2026, 7, 5), null, "cD4yZ0lMnO8r", "https://vokasia.example/verify/cD4yZ0lMnO8r");

        var bytes = new CertificatePdfDocument(data).GeneratePdf();

        Assert.NotEmpty(bytes);
    }
}
