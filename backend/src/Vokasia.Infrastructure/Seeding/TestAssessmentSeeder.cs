using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Seeding;

/// <summary>
/// Seeder khusus Test/E2E yang mensimulasikan berbagai status penilaian siswa (Mid-term, Final/Lulus).
/// Hanya dipanggil saat environment "Testing" atau trigger test suite eksplisit.
/// Dev & Production TETAP KOSONG.
/// </summary>
public static class TestAssessmentSeeder
{
    public static async Task SeedTestAssessmentsAsync(VokasiaDbContext db, CancellationToken ct = default)
    {
        var student1 = await db.Students.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.Nisn == "20250001", ct);
        var student2 = await db.Students.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.Nisn == "20250002", ct);

        if (student1 is null || student2 is null) return;

        var placement1 = await db.Placements.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.StudentId == student1.Id, ct);
        var placement2 = await db.Placements.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.StudentId == student2.Id, ct);

        if (placement1 is null || placement2 is null) return;

        // Snapshot dibaca dari placement1 & placement2
        var snapshot1 = await db.PlacementLearningRecordSnapshots.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlacementId == placement1.Id, ct);
        var snapshot2 = await db.PlacementLearningRecordSnapshots.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlacementId == placement2.Id, ct);

        var competency = await db.Competencies.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == placement2.TenantId, ct);

        // 1. Siswa 1: Penilaian Tengah Selesai (Mid-term Completed)
        if (snapshot1 is not null)
        {
            var assessmentMid = new LearningAssessment
            {
                Id = Guid.NewGuid(),
                TenantId = placement1.TenantId,
                PlacementId = placement1.Id,
                SnapshotId = snapshot1.Id,
                Stage = LearningAssessmentStage.Middle,
                Status = LearningAssessmentStatus.Finalized,
            };
            db.LearningAssessments.Add(assessmentMid);

            var revisionMid = LearningAssessmentRevision.Create(
                placement1.TenantId,
                assessmentMid.Id,
                placement1.Id,
                snapshot1.Id,
                LearningAssessmentStage.Middle,
                Guid.Empty,
                "Mentor Dev",
                "Siswa menunjukkan progres baik di pertengahan PKL.",
                DateTimeOffset.UtcNow.AddDays(-30),
                []);
            db.LearningAssessmentRevisions.Add(revisionMid);
            assessmentMid.LatestFinalizedRevisionId = revisionMid.Id;
        }

        // 2. Siswa 2: Penilaian Akhir Selesai / Lulus (Final Completed) + Approved Journals + Certificate
        if (snapshot2 is not null)
        {
            // Tambahkan Slot & Jurnal Approved
            var slot = await db.JournalSlots.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.PlacementId == placement2.Id, ct);
            if (slot is null)
            {
                slot = new JournalSlot
                {
                    Id = Guid.NewGuid(),
                    TenantId = placement2.TenantId,
                    PlacementId = placement2.Id,
                    Date = AppTimeZone.TodayJakarta().AddDays(-5),
                    Status = JournalSlotStatus.Filled
                };
                db.JournalSlots.Add(slot);
            }
            else
            {
                slot.Status = JournalSlotStatus.Filled;
            }

            var entry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = placement2.TenantId,
                SlotId = slot.Id,
                PlacementId = placement2.Id,
                Text = "<p>Mengerjakan tugas akhir modul backend dan integrasi API.</p>",
                Status = JournalEntryStatus.Approved,
                SubmittedAt = DateTimeOffset.UtcNow.AddDays(-5),
                ApprovedAt = DateTimeOffset.UtcNow.AddDays(-4)
            };
            db.JournalEntries.Add(entry);

            if (competency is not null)
            {
                db.JournalCompetencies.Add(new JournalCompetency
                {
                    JournalEntryId = entry.Id,
                    CompetencyId = competency.Id
                });
            }
            // Tambahkan Assessment Final V3
            var assessmentFinal = new LearningAssessment
            {
                Id = Guid.NewGuid(),
                TenantId = placement2.TenantId,
                PlacementId = placement2.Id,
                SnapshotId = snapshot2.Id,
                Stage = LearningAssessmentStage.Final,
                Status = LearningAssessmentStatus.Finalized,
            };
            db.LearningAssessments.Add(assessmentFinal);

            var revisionFinal = LearningAssessmentRevision.Create(
                placement2.TenantId,
                assessmentFinal.Id,
                placement2.Id,
                snapshot2.Id,
                LearningAssessmentStage.Final,
                Guid.Empty,
                "Mentor Dev",
                "Siswa telah menyelesaikan seluruh target PKL dan LULUS dengan nilai memuaskan.",
                DateTimeOffset.UtcNow,
                []);
            db.LearningAssessmentRevisions.Add(revisionFinal);
            assessmentFinal.LatestFinalizedRevisionId = revisionFinal.Id;
            placement2.Status = PlacementStatus.Completed;

            // Tambahkan Certificate
            var certificate = new Certificate
            {
                Id = Guid.NewGuid(),
                TenantId = placement2.TenantId,
                PlacementId = placement2.Id,
                CertCode = "CERT-2026-RPL-001",
                IssuedAt = DateTimeOffset.UtcNow
            };
            db.Certificates.Add(certificate);
            var portfolio = new Portfolio
            {
                Id = Guid.NewGuid(),
                TenantId = placement2.TenantId,
                StudentId = student2.Id,
                Headline = "Junior Web Developer - Expert in C# and React",
                IsPublished = true,
                Slug = "siswa-2-portfolio",
                SampleJournalIdsCsv = entry.Id.ToString()
            };
            db.Portfolios.Add(portfolio);
        }

        await db.SaveChangesAsync(ct);
    }
}
