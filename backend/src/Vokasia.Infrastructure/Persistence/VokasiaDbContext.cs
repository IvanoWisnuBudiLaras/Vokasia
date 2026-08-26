using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Infrastructure.Persistence;

/// <summary>
/// Satu DbContext untuk seluruh platform (Identity + OpenIddict store + domain entities).
/// Skema ini terbuka untuk pengembangan dan revisi.
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
    public DbSet<PaymentSubmission> PaymentSubmissions => Set<PaymentSubmission>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MentorInvite> MentorInvites => Set<MentorInvite>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<LearningRecordTemplate> LearningRecordTemplates => Set<LearningRecordTemplate>();
    public DbSet<LearningRecordTemplateCriterion> LearningRecordTemplateCriteria => Set<LearningRecordTemplateCriterion>();
    public DbSet<PlacementLearningRecordSnapshot> PlacementLearningRecordSnapshots => Set<PlacementLearningRecordSnapshot>();
    public DbSet<PlacementLearningRecordCriterionSnapshot> PlacementLearningRecordCriterionSnapshots => Set<PlacementLearningRecordCriterionSnapshot>();
    public DbSet<LearningAssessment> LearningAssessments => Set<LearningAssessment>();
    public DbSet<LearningAssessmentDraftCriterion> LearningAssessmentDraftCriteria => Set<LearningAssessmentDraftCriterion>();
    public DbSet<LearningAssessmentRevision> LearningAssessmentRevisions => Set<LearningAssessmentRevision>();
    public DbSet<LearningAssessmentRevisionCriterion> LearningAssessmentRevisionCriteria => Set<LearningAssessmentRevisionCriterion>();
    public DbSet<LearningAssessmentRevisionCriterionEvidence> LearningAssessmentRevisionCriterionEvidence => Set<LearningAssessmentRevisionCriterionEvidence>();
    public DbSet<LearningAssessmentCriterionEvidence> LearningAssessmentCriterionEvidence => Set<LearningAssessmentCriterionEvidence>();
    public DbSet<TeacherMonitoringEvent> TeacherMonitoringEvents => Set<TeacherMonitoringEvent>();
    public DbSet<AssessmentReminderDelivery> AssessmentReminderDeliveries => Set<AssessmentReminderDelivery>();

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
            e.Property(x => x.Text).HasMaxLength(12_000);
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

        b.Entity<RubricTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CompanyId, x.IsActive, x.Version });
            e.Property(x => x.Name).HasMaxLength(200);
        });
        b.Entity<RubricAspect>(e =>
        {
            e.HasIndex(x => x.RubricTemplateId);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
        });
        b.Entity<Assessment>(e => e.HasIndex(x => x.PlacementId));
        b.Entity<AssessmentScore>(e =>
        {
            e.HasIndex(x => x.AssessmentId);
            e.Property(x => x.Comment).HasMaxLength(2000);
        });

        b.Entity<LearningRecordTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CompanyId, x.Status, x.Version });
            e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LearningRecordTemplateCriterion>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.SortOrder }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
        });
        b.Entity<PlacementLearningRecordSnapshot>(e =>
        {
            e.HasIndex(x => x.PlacementId).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CompanyId });
            e.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LearningRecordTemplate>().WithMany().HasForeignKey(x => x.SourceTemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PlacementLearningRecordCriterionSnapshot>(e =>
        {
            e.HasIndex(x => new { x.SnapshotId, x.SortOrder }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
        });
        b.Entity<LearningAssessment>(e =>
        {
            e.HasIndex(x => new { x.PlacementId, x.Stage }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.SnapshotId);
            e.Property(x => x.OverallNote).HasMaxLength(2000);
            e.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PlacementLearningRecordSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LatestFinalizedRevision).WithMany().HasForeignKey(x => x.LatestFinalizedRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.DraftCriteria).WithOne().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningAssessmentDraftCriterion>(e =>
        {
            e.HasIndex(x => new { x.AssessmentId, x.CriterionSnapshotId }).IsUnique();
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne<PlacementLearningRecordCriterionSnapshot>().WithMany().HasForeignKey(x => x.CriterionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.DraftCriterionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LearningAssessmentRevision>(e =>
        {
            e.HasIndex(x => new { x.AssessmentId, x.FinalizedAt });
            e.HasIndex(x => new { x.PlacementId, x.Stage });
            e.Property(x => x.EvaluatorDisplayName).HasMaxLength(200);
            e.Property(x => x.OverallNote).HasMaxLength(2000);
            e.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PlacementLearningRecordSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Criteria).HasField("_criteria").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<LearningAssessmentRevisionCriterion>(e =>
        {
            e.HasIndex(x => new { x.RevisionId, x.CriterionSnapshotId }).IsUnique();
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne<PlacementLearningRecordCriterionSnapshot>().WithMany().HasForeignKey(x => x.CriterionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.RevisionCriterionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Evidence).HasField("_evidence").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<LearningAssessmentRevisionCriterionEvidence>(e =>
        {
            e.HasIndex(x => x.RevisionCriterionId);
            e.HasIndex(x => x.JournalEntryId);
            e.Property(x => x.Text).HasMaxLength(12_000);
            e.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LearningAssessmentCriterionEvidence>(e =>
        {
            e.HasIndex(x => x.DraftCriterionId);
            e.HasIndex(x => x.JournalEntryId);
            e.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TeacherMonitoringEvent>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PlacementId, x.Status });
            e.HasIndex(x => new { x.TeacherUserId, x.CreatedAt });
            e.Property(x => x.Note).HasMaxLength(2000);
            e.Property(x => x.FollowUpContext).HasMaxLength(2000);
            e.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Visit>().WithMany().HasForeignKey(x => x.FollowUpVisitId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AssessmentReminderDelivery>(e =>
        {
            e.HasIndex(x => new { x.PlacementId, x.Stage, x.ReminderType, x.RecipientUserId }).IsUnique();
            e.HasIndex(x => x.TenantId);
            e.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Placement>(e => e.HasIndex(x => new { x.TenantId, x.StudentId }));

        b.Entity<Certificate>(e => e.HasIndex(x => x.CertCode).IsUnique());
        b.Entity<Portfolio>(e => e.HasIndex(x => x.Slug).IsUnique());

        b.Entity<Plan>();
        b.Entity<FeatureFlag>(e => e.HasIndex(x => new { x.TenantId, x.PlanId, x.Key }));
        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PeriodMonth });
            e.HasIndex(x => new { x.TenantId, x.Status });
        });
        b.Entity<PaymentSubmission>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.InvoiceId });
            e.HasIndex(x => new { x.InvoiceId, x.SubmittedAt });
        });
        b.Entity<Subscription>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

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
        b.Entity<PaymentSubmission>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<Subscription>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningRecordTemplate>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningRecordTemplateCriterion>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<PlacementLearningRecordSnapshot>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<PlacementLearningRecordCriterionSnapshot>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessment>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessmentDraftCriterion>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessmentRevision>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessmentRevisionCriterion>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessmentRevisionCriterionEvidence>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<LearningAssessmentCriterionEvidence>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<TeacherMonitoringEvent>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        b.Entity<AssessmentReminderDelivery>().HasQueryFilter(x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
        // Company dan Plan global. FeatureFlag campuran plan-level/tenant override dan difilter
        // secara eksplisit oleh resolver karena TenantId-nya nullable.
    }
}
