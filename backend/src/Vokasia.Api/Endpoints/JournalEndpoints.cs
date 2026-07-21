using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H3-E1: siklus jurnal harian siswa + approval mentor + komentar guru. Slot dibuat cron
/// (Vokasia.Worker/Jobs/JournalCronJobs, §1) — endpoint di sini TIDAK pernah membuat JournalSlot
/// baru sendiri, hanya mengonsumsi/mengisi slot yang sudah ada.
///
/// Bucket MinIO: nama dibaca dari config "Minio:Bucket" (fallback "vokasia-journal") — di-ensure
/// exist sekali saat startup (Program.cs, idempoten spt SeedOAuthClientsAsync), bukan tiap request
/// (hindari latency HEAD-bucket per panggilan presign).
/// </summary>
public static class JournalEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;
    private const int MaxPhotosPerEntry = 3;
    private const int MaxCompetenciesPerEntry = 5;

    public static IEndpointRouteBuilder MapJournalEndpoints(this IEndpointRouteBuilder app)
    {
        var journals = app.MapGroup("/api/journals").WithTags("Journals");

        // --- §2 siswa — StudentSelf ---
        journals.MapGet("/today", GetTodayJournal).RequireAuthorization(RbacPolicies.StudentSelf);
        journals.MapPost("/{slotId:guid}/submit", SubmitJournal).RequireAuthorization(RbacPolicies.StudentSelf);
        journals.MapPost("/upload-url", GetPresignedUploadUrl).RequireAuthorization(RbacPolicies.StudentSelf);
        journals.MapPost("/{id:guid}/photos", AttachPhoto).RequireAuthorization(RbacPolicies.StudentSelf);
        journals.MapGet("/", ListJournals).RequireAuthorization(RbacPolicies.StudentSelf);

        // --- §3 mentor — MentorOwnPlacement ---
        // SENGAJA TIDAK `.RequireAuthorization(RbacPolicies.MentorOwnPlacement)` di level route:
        // PlacementScopeHandler adalah AuthorizationHandler<TRequirement, Placement> (resource-based)
        // — di level route, context.Resource defaultnya HttpContext (bukan Placement), requirement
        // TIDAK PERNAH bisa Succeed (base class hanya panggil HandleRequirementAsync kalau
        // context.Resource is TResource) -> semua request mentor akan selalu 403 kalau policy penuh
        // dipasang di sini. Maka: route cuma butuh authenticated (peran IndustryMentor ditegakkan
        // ULANG scr eksplisit di dalam handler lewat IAuthorizationService.AuthorizeAsync dgn
        // resource Placement yang BENAR — itulah yang dirancang komentar PlacementScopeHandler
        // sendiri: "mulai dipakai H3 ApproveJournal dst").
        journals.MapGet("/pending", GetPendingApprovals).RequireAuthorization();
        journals.MapPost("/{id:guid}/approve", ApproveJournal).RequireAuthorization();
        journals.MapPost("/{id:guid}/reject", RejectJournal).RequireAuthorization();
        journals.MapPost("/batch-approve", BatchApprove).RequireAuthorization();

        // --- §4 guru — TeacherPlus ---
        journals.MapPost("/{id:guid}/comments", AddTeacherComment).RequireAuthorization(RbacPolicies.TeacherPlus);

        var competencies = app.MapGroup("/api/competencies").WithTags("Journals");
        competencies.MapGet("/", ListCompetencies).RequireAuthorization(RbacPolicies.TeacherPlus);

        return app;
    }

    private static async Task<(JournalEntry? Entry, Placement? Placement)> LoadEntryWithPlacementAsync(VokasiaDbContext db, Guid entryId, CancellationToken ct)
    {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null)
        {
            return (null, null);
        }
        // IndustryMentor TIDAK punya tenant_id (lintas-tenant by design) -> query filter tenant
        // otomatis "mati" utk request ini (ambient TenantId null), Placement lintas tenant manapun
        // bisa ditemukan di sini - itulah kenapa PlacementScopeHandler (bukan filter tenant) yang
        // JADI satu-satunya gerbang keamanan resource ini.
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == entry.PlacementId, ct);
        return (entry, placement);
    }

    // ---------- helpers ----------

    private static async Task<Placement?> ResolveActivePlacementAsync(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.UserId.HasValue)
        {
            return null;
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == tenant.UserId, ct);
        if (student is null)
        {
            return null;
        }

        return await db.Placements.FirstOrDefaultAsync(p => p.StudentId == student.Id && p.Status == PlacementStatus.Active, ct);
    }

    private static JournalDto ToDto(JournalEntry e, List<PhotoDto> photos, List<Guid> competencyIds) =>
        new(e.Id, e.SlotId, e.PlacementId, e.Text, e.Status, e.MentorNote, e.SubmittedAt, e.ApprovedAt, photos, competencyIds);

    // ---------- §2 siswa ----------

    private static async Task<IResult> GetTodayJournal(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var placement = await ResolveActivePlacementAsync(db, tenant, ct);
        if (placement is null)
        {
            return Results.NotFound(new { message = "Tidak ada placement aktif untuk akun ini." });
        }

        var today = AppTimeZone.TodayJakarta();
        var slot = await db.JournalSlots.FirstOrDefaultAsync(s => s.PlacementId == placement.Id && s.Date == today, ct);
        if (slot is null)
        {
            return Results.NotFound(new { message = "Belum ada slot jurnal untuk hari ini (cron 05:00 WIB belum jalan atau hari ini libur)." });
        }

        JournalDto? entryDto = null;
        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.SlotId == slot.Id, ct);
        if (entry is not null)
        {
            var photos = await db.JournalPhotos.Where(p => p.JournalEntryId == entry.Id)
                .Select(p => new PhotoDto(p.Id, p.ObjectKey, p.ThumbKey, p.Status)).ToListAsync(ct);
            var compIds = await db.JournalCompetencies.Where(jc => jc.JournalEntryId == entry.Id)
                .Select(jc => jc.CompetencyId).ToListAsync(ct);
            entryDto = ToDto(entry, photos, compIds);
        }

        var major = await db.Students.AsNoTracking().Where(s => s.Id == placement.StudentId).Select(s => s.MajorId).FirstOrDefaultAsync(ct);
        var competencies = await db.Competencies.AsNoTracking().Where(c => c.MajorId == major)
            .Select(c => new CompetencyDto(c.Id, c.Name, c.MajorId)).ToListAsync(ct);

        // Senin minggu berjalan (ISO, Senin=awal) s.d. Jumat - cerminan hari kerja PKL (cron sendiri skip Sabtu/Minggu).
        var diffToMonday = ((int)today.DayOfWeek + 6) % 7; // Senin=0 ... Minggu=6
        var monday = today.AddDays(-diffToMonday);
        var weekDates = Enumerable.Range(0, 5).Select(i => monday.AddDays(i)).ToList();
        var weekSlots = await db.JournalSlots
            .Where(s => s.PlacementId == placement.Id && weekDates.Contains(s.Date))
            .ToDictionaryAsync(s => s.Date, s => s.Status, ct);
        var weekStatus = weekDates
            .Select(d => new WeekDayStatusDto(d, weekSlots.TryGetValue(d, out var st) ? st : JournalSlotStatus.Empty))
            .ToList();

        var streak = await db.StudentDailyStatuses.AsNoTracking()
            .Where(x => x.StudentId == placement.StudentId && x.PeriodId == placement.PeriodId)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Streak)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new TodayJournalDto(new JournalSlotDto(slot.Id, slot.Date, slot.Status), entryDto, competencies, weekStatus, streak));
    }

    private static async Task<IResult> SubmitJournal(Guid slotId, SubmitJournalRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (req.Text.Length > 500)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = ["Teks jurnal maksimal 500 karakter."] });
        }
        if (req.CompetencyIds.Count > MaxCompetenciesPerEntry)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["CompetencyIds"] = [$"Maksimal {MaxCompetenciesPerEntry} kompetensi per jurnal."] });
        }
        if (req.PhotoIds is { Count: > MaxPhotosPerEntry })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["PhotoIds"] = [$"Maksimal {MaxPhotosPerEntry} foto per jurnal."] });
        }

        var placement = await ResolveActivePlacementAsync(db, tenant, ct);
        if (placement is null)
        {
            return Results.Forbid();
        }

        var slot = await db.JournalSlots.FirstOrDefaultAsync(s => s.Id == slotId, ct);
        if (slot is null || slot.PlacementId != placement.Id)
        {
            return Results.Forbid(); // slot bukan milik sendiri.
        }

        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.SlotId == slotId, ct);
        if (entry is not null && entry.Status != JournalEntryStatus.Rejected)
        {
            return Results.Conflict(new { message = "Slot sudah terisi." });
        }

        var isResubmit = entry is not null;
        if (entry is null)
        {
            entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = placement.TenantId, SlotId = slotId, PlacementId = placement.Id };
            db.JournalEntries.Add(entry);
        }
        entry.Text = req.Text;
        entry.Status = JournalEntryStatus.Submitted;
        entry.MentorNote = null;
        entry.SubmittedAt = DateTimeOffset.UtcNow;
        entry.ApprovedAt = null;

        if (isResubmit)
        {
            var oldLinks = db.JournalCompetencies.Where(jc => jc.JournalEntryId == entry.Id);
            db.JournalCompetencies.RemoveRange(oldLinks);
        }
        foreach (var competencyId in req.CompetencyIds.Distinct())
        {
            db.JournalCompetencies.Add(new JournalCompetency { JournalEntryId = entry.Id, CompetencyId = competencyId });
        }

        slot.Status = JournalSlotStatus.Filled;

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "JournalSubmitted",
            PayloadJson = JsonSerializer.Serialize(new { entry.Id, entry.SlotId, entry.PlacementId, Resubmit = isResubmit }),
        });

        await db.SaveChangesAsync(ct);

        var photos = await db.JournalPhotos.Where(p => p.JournalEntryId == entry.Id)
            .Select(p => new PhotoDto(p.Id, p.ObjectKey, p.ThumbKey, p.Status)).ToListAsync(ct);
        return Results.Ok(ToDto(entry, photos, req.CompetencyIds.Distinct().ToList()));
    }

    private static async Task<IResult> GetPresignedUploadUrl(UploadRequest req, IMinioClient minio, ITenantContext tenant, IConfiguration config, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }
        if (!AllowedContentTypes.Contains(req.ContentType))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ContentType"] = ["Tipe berkas hanya image/jpeg, image/png, atau image/webp."] });
        }
        if (req.SizeBytes > MaxPhotoSizeBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["SizeBytes"] = ["Ukuran berkas maksimal 5MB."] });
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var extension = req.ContentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin",
        };
        var objectKey = $"tenant/{tenant.TenantId}/journal/{Guid.NewGuid():N}.{extension}";
        const int expirySeconds = 300;

        var args = new PresignedPutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds);
        var url = await minio.PresignedPutObjectAsync(args);

        return Results.Ok(new PresignedUploadDto(url, objectKey, expirySeconds));
    }

    private static async Task<IResult> AttachPhoto(Guid id, AttachPhotoRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var placement = await ResolveActivePlacementAsync(db, tenant, ct);
        if (placement is null)
        {
            return Results.Forbid();
        }

        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null || entry.PlacementId != placement.Id)
        {
            return Results.NotFound();
        }

        var existingCount = await db.JournalPhotos.CountAsync(p => p.JournalEntryId == id, ct);
        if (existingCount >= MaxPhotosPerEntry)
        {
            return Results.Conflict(new { message = $"Maksimal {MaxPhotosPerEntry} foto per jurnal." });
        }

        var photo = new JournalPhoto { Id = Guid.NewGuid(), TenantId = placement.TenantId, JournalEntryId = id, ObjectKey = req.ObjectKey, Status = PhotoStatus.Pending };
        db.JournalPhotos.Add(photo);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "PhotoUploaded",
            PayloadJson = JsonSerializer.Serialize(new { photo.Id, photo.JournalEntryId, photo.ObjectKey }),
        });
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/journals/{id}/photos/{photo.Id}", new PhotoDto(photo.Id, photo.ObjectKey, photo.ThumbKey, photo.Status));
    }

    private static async Task<IResult> ListJournals(
        VokasiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [FromQuery] Guid? placementId = null, [FromQuery] JournalEntryStatus? status = null,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == tenant.UserId, ct);
        if (student is null)
        {
            return Results.Forbid();
        }

        var ownPlacementIds = await db.Placements.AsNoTracking().Where(p => p.StudentId == student.Id).Select(p => p.Id).ToListAsync(ct);
        if (placementId.HasValue && !ownPlacementIds.Contains(placementId.Value))
        {
            return Results.Forbid();
        }
        var scopedPlacementIds = placementId.HasValue ? [placementId.Value] : ownPlacementIds;

        var query = db.JournalEntries.AsNoTracking().Where(e => scopedPlacementIds.Contains(e.PlacementId));
        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }
        if (from.HasValue || to.HasValue)
        {
            var slotIdsInRange = db.JournalSlots.Where(s =>
                (!from.HasValue || s.Date >= from.Value) && (!to.HasValue || s.Date <= to.Value)).Select(s => s.Id);
            query = query.Where(e => slotIdsInRange.Contains(e.SlotId));
        }

        var total = await query.CountAsync(ct);
        // Proyeksi langsung + subquery berkorelasi utk Photos/CompetencyIds - EF Core menerjemahkan
        // ini jadi 1 query utama (LEFT JOIN/subquery correlated), BUKAN N+1 round-trip terpisah per
        // baris (AC: "1 query utama, log EF dibuktikan") - dibuktikan tuntas thd Postgres nyata,
        // lihat DECISIONS.md.
        var items = await query
            .OrderByDescending(e => e.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new JournalDto(
                e.Id, e.SlotId, e.PlacementId, e.Text, e.Status, e.MentorNote, e.SubmittedAt, e.ApprovedAt,
                db.JournalPhotos.Where(p => p.JournalEntryId == e.Id).Select(p => new PhotoDto(p.Id, p.ObjectKey, p.ThumbKey, p.Status)).ToList(),
                db.JournalCompetencies.Where(jc => jc.JournalEntryId == e.Id).Select(jc => jc.CompetencyId).ToList()))
            .ToListAsync(ct);

        return Results.Ok(new Paged<JournalDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> ListCompetencies([FromQuery] Guid majorId, VokasiaDbContext db, CancellationToken ct)
    {
        var items = await db.Competencies.AsNoTracking().Where(c => c.MajorId == majorId)
            .Select(c => new CompetencyDto(c.Id, c.Name, c.MajorId)).ToListAsync(ct);
        return Results.Ok(items);
    }

    // ---------- §3 mentor ----------

    private static async Task<IResult> GetPendingApprovals(System.Security.Claims.ClaimsPrincipal user, VokasiaDbContext db, CancellationToken ct)
    {
        var sub = user.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject)?.Value;
        if (!Guid.TryParse(sub, out var mentorId))
        {
            return Results.Forbid();
        }

        // List (bukan aksi 1 resource) - scoping lewat WHERE langsung (AuthorizeAsync per-item tak
        // relevan utk operasi baca banyak baris); PlacementScopeHandler tetap satu2nya gerbang utk
        // MUTASI (Approve/Reject/BatchApprove) di bawah.
        var pending = await db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Submitted)
            .Join(db.Placements.Where(p => p.MentorUserId == mentorId), e => e.PlacementId, p => p.Id, (e, p) => new { Entry = e, p.StudentId })
            .Join(db.Students, x => x.StudentId, s => s.Id, (x, s) => new { x.Entry, s.Id, s.FullName })
            .ToListAsync(ct);

        var grouped = pending
            .GroupBy(x => (x.Id, x.FullName))
            .Select(g => new PendingGroupDto(
                g.Key.Id, g.Key.FullName,
                g.Select(x => ToDto(x.Entry, [], [])).ToList())) // ringkasan approval TIDAK perlu foto/kompetensi penuh - AC: "layar W2" cukup teks+status.
            .ToList();

        return Results.Ok(grouped);
    }

    private static async Task<IResult> ApproveJournal(
        Guid id, ApproveJournalRequest req, System.Security.Claims.ClaimsPrincipal user,
        IAuthorizationService authService, VokasiaDbContext db, CancellationToken ct)
    {
        var (entry, placement) = await LoadEntryWithPlacementAsync(db, id, ct);
        if (entry is null || placement is null)
        {
            return Results.NotFound();
        }

        var authResult = await authService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        if (entry.Status != JournalEntryStatus.Submitted)
        {
            return Results.Conflict(new { message = "Jurnal ini bukan berstatus menunggu persetujuan." });
        }

        entry.EnsureMutable(); // AC §1: hook wajib dipanggil - penegakan penuh dipasang H3-E3.
        entry.Status = JournalEntryStatus.Approved;
        entry.ApprovedAt = DateTimeOffset.UtcNow;
        entry.MentorNote = req.Note;

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "JournalApproved",
            PayloadJson = JsonSerializer.Serialize(new { entry.Id, entry.PlacementId, entry.SlotId }),
        });

        await db.SaveChangesAsync(ct);
        var photos = await db.JournalPhotos.Where(p => p.JournalEntryId == entry.Id).Select(p => new PhotoDto(p.Id, p.ObjectKey, p.ThumbKey, p.Status)).ToListAsync(ct);
        var compIds = await db.JournalCompetencies.Where(jc => jc.JournalEntryId == entry.Id).Select(jc => jc.CompetencyId).ToListAsync(ct);
        return Results.Ok(ToDto(entry, photos, compIds));
    }

    private static async Task<IResult> RejectJournal(
        Guid id, RejectJournalRequest req, System.Security.Claims.ClaimsPrincipal user,
        IAuthorizationService authService, VokasiaDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Alasan penolakan wajib diisi."] });
        }

        var (entry, placement) = await LoadEntryWithPlacementAsync(db, id, ct);
        if (entry is null || placement is null)
        {
            return Results.NotFound();
        }

        var authResult = await authService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        if (entry.Status != JournalEntryStatus.Submitted)
        {
            return Results.Conflict(new { message = "Jurnal ini bukan berstatus menunggu persetujuan." });
        }

        entry.EnsureMutable();
        entry.Status = JournalEntryStatus.Rejected;
        entry.MentorNote = req.Reason;
        // Slot TETAP Filled (bukan direset Empty) - siswa isi ulang lewat SubmitJournal yang
        // sudah menangani "entry.Status == Rejected -> update in-place" (lihat SubmitJournal di atas).

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "JournalRejected",
            PayloadJson = JsonSerializer.Serialize(new { entry.Id, entry.PlacementId, entry.SlotId, req.Reason }),
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(entry, [], []));
    }

    private static async Task<IResult> BatchApprove(BatchApproveRequest req, System.Security.Claims.ClaimsPrincipal user, IAuthorizationService authService, VokasiaDbContext db, CancellationToken ct)
    {
        var approved = new List<Guid>();
        var failed = new List<BatchFailure>();

        foreach (var id in req.Ids)
        {
            var (entry, placement) = await LoadEntryWithPlacementAsync(db, id, ct);
            if (entry is null || placement is null)
            {
                failed.Add(new BatchFailure(id, "Jurnal tidak ditemukan."));
                continue;
            }

            var authResult = await authService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
            if (!authResult.Succeeded)
            {
                failed.Add(new BatchFailure(id, "Bukan placement bimbingan Anda."));
                continue;
            }

            if (entry.Status != JournalEntryStatus.Submitted)
            {
                failed.Add(new BatchFailure(id, "Bukan berstatus menunggu persetujuan."));
                continue;
            }

            entry.EnsureMutable();
            entry.Status = JournalEntryStatus.Approved;
            entry.ApprovedAt = DateTimeOffset.UtcNow;

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "JournalApproved",
                PayloadJson = JsonSerializer.Serialize(new { entry.Id, entry.PlacementId, entry.SlotId }),
            });
            approved.Add(id);
        }

        // Satu SaveChanges di akhir: entitas yang GAGAL validasi di atas tak pernah diubah
        // propertinya sama sekali (continue sebelum mutasi), jadi aman - bukan butuh transaksi
        // per-item terpisah utk memenuhi "satu gagal tak membatalkan lainnya" (AC).
        await db.SaveChangesAsync(ct);

        return Results.Ok(new BatchResult(approved, failed));
    }

    // ---------- §4 guru ----------

    private static async Task<IResult> AddTeacherComment(Guid id, AddCommentRequest req, System.Security.Claims.ClaimsPrincipal user, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
        {
            return Results.NotFound();
        }

        var comment = new TeacherComment { Id = Guid.NewGuid(), TenantId = entry.TenantId, JournalEntryId = id, TeacherId = tenant.UserId.Value, Text = req.Text };
        db.TeacherComments.Add(comment);

        // Notifikasi siswa pemilik jurnal (FR-JRN-05).
        var studentUserId = await db.Placements.Where(p => p.Id == entry.PlacementId)
            .Join(db.Students, p => p.StudentId, s => s.Id, (p, s) => s.UserId)
            .FirstOrDefaultAsync(ct);
        if (studentUserId.HasValue)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = studentUserId.Value,
                Type = "TeacherComment",
                PayloadJson = JsonSerializer.Serialize(new { EntryId = entry.Id, CommentId = comment.Id }),
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/journals/{id}/comments/{comment.Id}", new CommentDto(comment.Id, comment.JournalEntryId, comment.TeacherId, comment.Text, comment.CreatedAt));
    }
}
