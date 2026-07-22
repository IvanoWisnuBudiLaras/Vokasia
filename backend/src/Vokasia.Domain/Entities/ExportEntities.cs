using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>
/// VOK-H5-E1 §4 — status permintaan export rekap nilai (FR-ASM-06, pola 202 Accepted). Baris
/// dibuat SAAT RequestExport dipanggil (Status=Requested), diupdate ExportRequestedConsumer
/// (Worker) setelah file jadi (Status=Completed + ObjectKey) atau gagal (Status=Failed).
/// </summary>
public class ExportRequest : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public ExportFormat Format { get; set; }
    public ExportStatus Status { get; set; } = ExportStatus.Requested;
    public string? ObjectKey { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
