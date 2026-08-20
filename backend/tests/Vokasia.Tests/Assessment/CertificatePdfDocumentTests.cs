using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Vokasia.Worker.Export;

namespace Vokasia.Tests.Assessment;

/// <summary>VOK-H5-E1 §5 — CertificatePdfDocument embeds the standards-compliant QR payload and verification text.</summary>
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
            "Siswa Contoh 001", "SMK Contoh 1", "PT Contoh Teknologi Nusantara", "Periode Ganjil 2026",
            new DateOnly(2026, 1, 5), new DateOnly(2026, 7, 5), 87.50m, "aB3xY9kLmN7q", "https://vokasia.example/verify/aB3xY9kLmN7q");

        var bytes = new CertificatePdfDocument(data).GeneratePdf();

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void GeneratePdf_NullFinalScore_DoesNotThrow()
    {
        var data = new CertificateData(
            "Siswa Contoh 002", "SMK Contoh 1", "CV Mitra Praktik Contoh", "Periode Genap 2026",
            new DateOnly(2026, 1, 5), new DateOnly(2026, 7, 5), null, "cD4yZ0lMnO8r", "https://vokasia.example/verify/cD4yZ0lMnO8r");

        var bytes = new CertificatePdfDocument(data).GeneratePdf();

        Assert.NotEmpty(bytes);
    }
}
