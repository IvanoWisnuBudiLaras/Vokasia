using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Infrastructure.Persistence;

/// <summary>
/// Satu DbContext untuk seluruh platform (Identity + OpenIddict store + domain entities).
/// Skema ini dibekukan gate M0 (PRD WBS 1.2) — perubahan pasca-beku = change control (PRD 3.6).
/// </summary>
public class VokasiaDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    private readonly ITenantContext _tenantContext;

    public VokasiaDbContext(DbContextOptions<VokasiaDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMergeHistory> CompanyMergeHistories => Set<CompanyMergeHistory>();
    public DbSet<TenantCompany> TenantCompanies => Set<TenantCompany>();
    public DbSet<CompanySlot> CompanySlots => Set<CompanySlot>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<Competency> Competencies => Set<Competency>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Placement> Placements => Set<Placement>();
    public DbSet<JournalSlot> JournalSlots => Set<JournalSlot>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalPhoto> JournalPhotos => Set<JournalPhoto>();
    public DbSet<JournalCompetency> JournalCompetencies => Set<JournalCompetency>();
    public DbSet<TeacherComment> TeacherComments => Set<TeacherComment>();
    public DbSet<StudentDailyStatus> StudentDailyStatuses => Set<StudentDailyStatus>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<RubricTemplate> RubricTemplates => Set<RubricTemplate>();
    public DbSet<RubricAspect> RubricAspects => Set<RubricAspect>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentScore> AssessmentScores => Set<AssessmentScore>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<ExportRequest> ExportRequests => Set<ExportRequest>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MentorInvite> MentorInvites => Set<MentorInvite>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

    /// <summary>
    /// VOK-H6-E3 §1 (FR-AUTH-07): "satu pintu" penegakan AC "audit log mencatat actor=SA, as=user"
    /// SELAMA impersonasi — TANPA menyentuh satu pun dari puluhan situs `db.AuditLogs.Add(...)` yang
    /// sudah tersebar di seluruh Endpoints/*.cs (semuanya menulis ActorUserId = ITenantContext.UserId
    /// milik pemanggil saat ini, yang SELAMA impersonasi = user TARGET, krn identity token sudah
    /// ditukar penuh — lihat TenantResolutionMiddleware). Di sinilah SATU-SATUNYA titik yang tahu
    /// ITenantContext.ImpersonatorUserId: sebelum SaveChanges betulan jalan, setiap AuditLog yang BARU
    /// ditambahkan (State==Added) di ChangeTracker dikoreksi — ActorUserId (target) dipindah ke
    /// ActingAsUserId, lalu ActorUserId diganti UserId SuperAdmin asli. Endpoint yang SUDAH secara
    /// eksplisit mengisi ActingAsUserId sendiri (mis. StartImpersonation menulis "ImpersonationStarted"
    /// langsung dgn actor SA + as=target, SEBELUM klaim impersonator_id ada utk request itu) tidak
    /// disentuh (guard ActingAsUserId is null) — mencegah dobel-koreksi.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_tenantContext.ImpersonatorUserId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<AuditLog>())
            {
                if (entry.State == EntityState.Added && entry.Entity.ActingAsUserId is null)
                {
                    entry.Entity.ActingAsUserId = entry.Entity.ActorUserId;
                    entry.Entity.ActorUserId = _tenantContext.ImpersonatorUserId.Value;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Npsn);
        });

        b.Entity<Company>();
        b.Entity<CompanyMergeHistory>();
        b.Entity<TenantCompany>(e => e.HasKey(x => new { x.TenantId, x.CompanyId }));
        b.Entity<CompanySlot>(e => e.HasIndex(x => new { x.TenantId, x.CompanyId, x.PeriodId }).IsUnique());

        b.Entity<Period>(e => e.HasIndex(x => x.TenantId));
        b.Entity<Holiday>(e => e.HasIndex(x => new { x.PeriodId, x.Date }));

        b.Entity<Major>();
        b.Entity<Competency>(e => e.HasIndex(x => x.MajorId));
        b.Entity<Student>(e => e.HasIndex(x => x.TenantId));

        b.Entity<Placement>(e =>
        {
            e.HasIndex(x => x.PeriodId);
            e.HasIndex(x => x.TenantId);
        });

        b.Entity<JournalSlot>(e =>
        {
            e.HasIndex(x => new { x.PlacementId, x.Date }).IsUnique();
        });
        b.Entity<JournalEntry>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(500);
            e.HasIndex(x => x.SlotId).IsUnique();
            e.HasIndex(x => new { x.PlacementId, x.Status });
        });
        b.Entity<JournalPhoto>(e => e.HasIndex(x => x.JournalEntryId));
        b.Entity<JournalCompetency>(e => e.HasKey(x => new { x.JournalEntryId, x.CompetencyId }));
        b.Entity<TeacherComment>(e => e.HasIndex(x => x.JournalEntryId));
        b.Entity<StudentDailyStatus>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.PeriodId, x.Date }).IsUnique();
            e.HasIndex(x => new { x.PeriodId, x.Date, x.Rag }); // query dashboard W3
        });

        b.Entity<Visit>(e => e.HasIndex(x => x.PlacementId));

        b.Entity<RubricTemplate>();
        b.Entity<RubricAspect>(e => e.HasIndex(x => x.RubricTemplateId));
        b.Entity<Assessment>(e => e.HasIndex(x => x.PlacementId));
        b.Entity<AssessmentScore>(e => e.HasIndex(x => x.AssessmentId));

        b.Entity<Certificate>(e => e.HasIndex(x => x.CertCode).IsUnique());
        b.Entity<Portfolio>(e => e.HasIndex(x => x.Slug).IsUnique());

        b.Entity<Plan>();
        b.Entity<FeatureFlag>(e => e.HasIndex(x => new { x.TenantId, x.PlanId, x.Key }));
        b.Entity<Invoice>(e => e.HasIndex(x => new { x.TenantId, x.PeriodMonth }).IsUnique());

        b.Entity<Notification>(e => e.HasIndex(x => new { x.UserId, x.IsRead }));
        b.Entity<AuditLog>(e => e.HasIndex(x => new { x.TenantId, x.CreatedAt }));

        b.Entity<OutboxMessage>(e => e.HasIndex(x => x.PublishedAt));
        b.Entity<ProcessedMessage>(e => e.HasKey(x => new { x.ConsumerName, x.MessageId }));
        // VOK-H4-E3 §2: unique -> DbUpdateException kalau 2 SaveChanges "menang" race bersamaan utk
        // kunci yg sama (lihat doc-comment IdempotentEmailSender utk penanganannya).
        b.Entity<SentEmail>(e => e.HasIndex(x => x.IdempotencyKey).IsUnique());

        // VOK-H2-E3 §3 (magic link mentor, slice ditunda-lalu-dikerjakan): TIDAK di-ApplyTenantQueryFilters
        // (tanpa kolom TenantId) — isolasi diwariskan dari Placement (lihat doc-comment MentorInvite).
        b.Entity<MentorInvite>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.PlacementId);
        });

        ApplyTenantQueryFilters(b);

        // OpenIddict store (VOK-H1-E3): tabel client/token/scope/authorization di DbContext yang sama.
        b.UseOpenIddict();
    }

    /// <summary>
    /// Global query filter tenant isolation (FR-AUTH-06). AKTIF PENUH sejak H2-E3:
    /// TenantResolutionMiddleware (Vokasia.Api/Auth) mengisi AmbientTenantContext dari claims JWT
    /// setiap request; DbContext ini membaca instance scoped yang SAMA (lihat DependencyInjection.cs).
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder b)
    {
        b.Entity<Period>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Holiday>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Major>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Competency>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Student>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<TenantCompany>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<CompanySlot>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Placement>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<JournalSlot>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<JournalEntry>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<JournalPhoto>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<TeacherComment>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<StudentDailyStatus>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Visit>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<RubricTemplate>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Assessment>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Certificate>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Portfolio>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<ExportRequest>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Invoice>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        // Company dan Plan global. FeatureFlag campuran plan-level/tenant override dan difilter
        // secara eksplisit oleh resolver karena TenantId-nya nullable.
    }
}
