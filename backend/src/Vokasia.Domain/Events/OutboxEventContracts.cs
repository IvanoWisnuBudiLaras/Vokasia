using System.Text.Json.Serialization;

namespace Vokasia.Domain.Events;

/// <summary>
/// VOK-H4-E1: kontrak deserialisasi <c>OutboxMessage.PayloadJson</c> dipakai <c>OutboxDispatcher</c>
/// (Vokasia.Infrastructure/Messaging) utk republish ke RabbitMQ via MassTransit, dan oleh consumer
/// (Vokasia.Worker/Consumers) utk baca payload ter-strong-type.
///
/// [TEMUAN penting, dicatat bukan diasumsikan]: ticket H4-E1 menulis bentuk field IDEAL utk tiap
/// event (mis. <c>JournalSubmitted{JournalId,StudentId,PeriodId,Date}</c>) - tapi PENULISAN outbox
/// utk KEENAM event ini SUDAH ADA & SUDAH BENAR sejak H2-E1/H2-E3/H3-E1 (db.OutboxMessages.Add(...)
/// inline, di transaksi yang SAMA dgn SaveChangesAsync perubahan data - guarantee "broker down !=
/// event hilang" SUDAH terpenuhi tanpa perlu SaveToOutboxInterceptor/domain-event-raising apa pun).
/// Bentuk field di bawah CERMIN PERSIS JSON yang BENAR-BENAR tersimpan (dikonfirmasi baca kode
/// nyata: JournalEndpoints.cs, CompaniesAndPlacements.cs, MagicLinkService.cs, JournalCronJobs.cs)
/// - BUKAN diubah mengikuti ticket, supaya nol risiko thd kode yang sudah teruji hijau. Field yang
/// TAK ADA di payload asli (mis. StudentId/PeriodId/Date di JournalSubmitted, StudentName/
/// CompanyName di MentorInvited) SENGAJA tidak dipaksakan ada di sini - consumer look-up sendiri
/// via query pakai Id/PlacementId/SlotId yang memang tersedia (event = pointer tipis, bukan
/// snapshot penuh - filosofi yang sama persis dgn JournalApproved versi ticket sendiri yang cuma
/// {JournalId,StudentId}).
///
/// [JsonPropertyName("Id")] dipakai utk beri nama C# yang lebih jelas (JournalId/PhotoId) drpd
/// "Id" mentah tanpa konteks, TANPA mengubah kunci JSON asli. Dispatcher deserialize dgn
/// PropertyNameCaseInsensitive=true (menutupi 1 inkonsistensi casing nyata yang ditemukan:
/// JournalReminderEmailRequested pakai camelCase eksplisit "userId"/"slotId", 6 lainnya PascalCase
/// implisit dari nama anggota C#) - tanpa perlu ubah kode penulis manapun.
/// </summary>
public record JournalSubmittedEvent(
    [property: JsonPropertyName("Id")] Guid JournalId,
    Guid SlotId,
    Guid PlacementId,
    bool Resubmit);

public record PhotoUploadedEvent(
    [property: JsonPropertyName("Id")] Guid PhotoId,
    Guid JournalEntryId,
    string ObjectKey);

public record JournalApprovedEvent(
    [property: JsonPropertyName("Id")] Guid JournalId,
    Guid PlacementId,
    Guid SlotId);

public record JournalRejectedEvent(
    [property: JsonPropertyName("Id")] Guid JournalId,
    Guid PlacementId,
    Guid SlotId,
    string Reason);

/// <summary>Payload nyata MagicLinkService.CreateInviteAsync — StudentName/CompanyName TIDAK ada
/// di sini (invite hanya tahu PlacementId), consumer look-up via join Placement->Student/Company.</summary>
public record MentorInvitedEvent(
    [property: JsonPropertyName("Id")] Guid InviteId,
    Guid PlacementId,
    string Email,
    DateTimeOffset ExpiresAt);

/// <summary>Payload nyata CreatePlacement/BulkCreatePlacements — TeacherId TIDAK ada di sini,
/// consumer look-up via query Placement by Id (PlacementId) utk dapat TeacherId+CompanyId+dst.</summary>
public record PlacementCreatedEvent(
    [property: JsonPropertyName("Id")] Guid PlacementId,
    Guid StudentId,
    Guid CompanyId,
    Guid PeriodId,
    string? MentorEmail);

/// <summary>
/// Payload nyata JournalCronJobs.RemindEmptyJournals — SATU-SATUNYA dari 7 yang pakai camelCase
/// eksplisit ("userId" bukan "UserId" dst, lihat komentar kelas di atas). TIDAK ada consumer utk
/// event ini di ticket H4-E1 (email sungguhan = H4-E3) - didaftarkan di sini SEMATA supaya
/// OutboxDispatcher tahu tipe CLR-nya (hindari "unknown type" log error tiap kali baris ini
/// dipublish tanpa consumer terpasang - itu memang perilaku BENAR/diharapkan, bukan bug, sampai
/// H4-E3 menambah consumer-nya).
/// </summary>
public record JournalReminderEmailRequestedEvent(Guid UserId, Guid SlotId, string StudentName, string Date);
