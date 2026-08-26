using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vokasia.Worker.Export;

public sealed record LearningRecordReportPdfRow(
    string StudentName,
    string CompanyName,
    string PeriodName,
    string MiddleStatus,
    string FinalStatus,
    string MonitoringStatus,
    string CompletionStatus);

/// <summary>Human-readable bounded V3 Learning Record report. This is a real PDF document, not browser print output.</summary>
public sealed class LearningRecordReportPdfDocument(
    string title,
    IReadOnlyList<LearningRecordReportPdfRow> rows) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.Header().Text(title).FontSize(16).Bold();
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    foreach (var heading in new[] { "Siswa", "DUDI", "Periode", "Middle", "Final", "Monitoring", "Status" })
                    {
                        header.Cell().Text(heading).Bold();
                    }
                });

                foreach (var row in rows)
                {
                    table.Cell().Text(row.StudentName);
                    table.Cell().Text(row.CompanyName);
                    table.Cell().Text(row.PeriodName);
                    table.Cell().Text(row.MiddleStatus);
                    table.Cell().Text(row.FinalStatus);
                    table.Cell().Text(row.MonitoringStatus);
                    table.Cell().Text(row.CompletionStatus);
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Halaman ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

}
