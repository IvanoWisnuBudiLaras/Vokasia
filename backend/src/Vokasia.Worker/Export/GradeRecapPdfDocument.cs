using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vokasia.Worker.Export;

/// <summary>VOK-H5-E1 §4 — satu baris rekap utk render tabel PDF export (proyeksi minimal, tanpa entity EF).</summary>
public sealed record GradeRecapPdfRow(string StudentName, string CompanyName, decimal? MentorAvg, decimal? TeacherAvg, decimal? FinalScore, string Status);

/// <summary>
/// VOK-H5-E1 §4 — export Pdf rekap nilai via QuestPDF (SUDAH pre-approved PRD.md baris 82, BEDA
/// dari Xlsx yang butuh MinimalXlsxWriter hand-rolled - lihat doc-comment kelas itu ttg gap
/// ClosedXML). Dokumen SEDERHANA (satu tabel, tanpa styling kompleks) - cukup utk AC ticket
/// ("file+notif <2 mnt utk 900 siswa"), bukan laporan resmi berkop surat (itu di luar cakupan
/// literal H5-E1).
/// </summary>
public sealed class GradeRecapPdfDocument(string periodName, IReadOnlyList<GradeRecapPdfRow> rows) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.Header().Text($"Rekap Nilai PKL — {periodName}").FontSize(16).Bold();
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Siswa
                    columns.RelativeColumn(3); // Perusahaan
                    columns.RelativeColumn(1); // Mentor avg
                    columns.RelativeColumn(1); // Teacher avg
                    columns.RelativeColumn(1); // Final
                    columns.RelativeColumn(1); // Status
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "Siswa", "Perusahaan", "Rata Mentor", "Rata Guru", "Nilai Akhir", "Status" })
                    {
                        header.Cell().Text(title).Bold();
                    }
                });

                foreach (var row in rows)
                {
                    table.Cell().Text(row.StudentName);
                    table.Cell().Text(row.CompanyName);
                    table.Cell().Text(row.MentorAvg?.ToString("0.00") ?? "-");
                    table.Cell().Text(row.TeacherAvg?.ToString("0.00") ?? "-");
                    table.Cell().Text(row.FinalScore?.ToString("0.00") ?? "-");
                    table.Cell().Text(row.Status);
                }
            });
            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Halaman ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
