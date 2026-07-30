using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Vokasia.Worker.Export;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// VOK-H5-E1 §4 — GradeRecapPdfDocument (QuestPDF, sudah pre-approved PRD.md). Membuktikan
/// dokumen BENAR2 ter-generate (byte PDF valid dgn header "%PDF", bukan cuma "tidak melempar
/// exception") - QuestPDF.Settings.License diset di sini scr eksplisit (test host TERPISAH dari
/// Vokasia.Worker/Program.cs yang normalnya men-set ini sekali di startup produksi).
/// </summary>
public class GradeRecapPdfDocumentTests
{
    static GradeRecapPdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_WithRows_ProducesValidPdfBytes()
    {
        var rows = new List<GradeRecapPdfRow>
        {
            new("Ahmad Fauzi", "PT Contoh Sejahtera", 85.00m, 90.00m, 87.00m, "Final"),
            new("Siti Aminah", "CV Berkah Jaya", 80.00m, null, null, "Draft"),
            new("Budi Santoso", "PT Contoh Sejahtera", null, null, null, "BelumDinilai"),
        };

        var bytes = new GradeRecapPdfDocument("Periode Ganjil 2026", rows).GeneratePdf();

        Assert.NotEmpty(bytes);
        var header = System.Text.Encoding.ASCII.GetString(bytes, 0, 5);
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void GeneratePdf_EmptyRows_StillProducesValidPdf()
    {
        var bytes = new GradeRecapPdfDocument("Periode Tanpa Siswa", []).GeneratePdf();

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
