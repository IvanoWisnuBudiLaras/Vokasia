using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningRecordFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    ReminderType = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentReminderDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentReminderDeliveries_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningRecordTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRecordTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningRecordTemplates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherMonitoringEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    FollowUpVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    FollowUpContext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherMonitoringEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherMonitoringEvents_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherMonitoringEvents_Visits_FollowUpVisitId",
                        column: x => x.FollowUpVisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningRecordTemplateCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRecordTemplateCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningRecordTemplateCriteria_LearningRecordTemplates_Temp~",
                        column: x => x.TemplateId,
                        principalTable: "LearningRecordTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlacementLearningRecordSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementLearningRecordSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementLearningRecordSnapshots_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlacementLearningRecordSnapshots_LearningRecordTemplates_So~",
                        column: x => x.SourceTemplateId,
                        principalTable: "LearningRecordTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlacementLearningRecordSnapshots_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlacementLearningRecordCriterionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementLearningRecordCriterionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementLearningRecordCriterionSnapshots_PlacementLearning~",
                        column: x => x.SnapshotId,
                        principalTable: "PlacementLearningRecordSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentCriterionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortfolioEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentCriterionEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentCriterionEvidence_JournalEntries_JournalE~",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentDraftCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentDraftCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentDraftCriteria_PlacementLearningRecordCrit~",
                        column: x => x.CriterionSnapshotId,
                        principalTable: "PlacementLearningRecordCriterionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentRevisionCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentRevisionCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentRevisionCriteria_PlacementLearningRecordC~",
                        column: x => x.CriterionSnapshotId,
                        principalTable: "PlacementLearningRecordCriterionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    EvaluatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatorDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OverallNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentRevisions_PlacementLearningRecordSnapshot~",
                        column: x => x.SnapshotId,
                        principalTable: "PlacementLearningRecordSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentRevisions_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EvaluatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverallNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LatestFinalizedRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessments_LearningAssessmentRevisions_LatestFinal~",
                        column: x => x.LatestFinalizedRevisionId,
                        principalTable: "LearningAssessmentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningAssessments_PlacementLearningRecordSnapshots_Snapsh~",
                        column: x => x.SnapshotId,
                        principalTable: "PlacementLearningRecordSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningAssessments_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Placements_TenantId_StudentId",
                table: "Placements",
                columns: new[] { "TenantId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentReminderDeliveries_PlacementId_Stage_ReminderType~",
                table: "AssessmentReminderDeliveries",
                columns: new[] { "PlacementId", "Stage", "ReminderType", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentReminderDeliveries_TenantId",
                table: "AssessmentReminderDeliveries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentCriterionEvidence_DraftCriterionId",
                table: "LearningAssessmentCriterionEvidence",
                column: "DraftCriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentCriterionEvidence_JournalEntryId",
                table: "LearningAssessmentCriterionEvidence",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentDraftCriteria_AssessmentId_CriterionSnaps~",
                table: "LearningAssessmentDraftCriteria",
                columns: new[] { "AssessmentId", "CriterionSnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentDraftCriteria_CriterionSnapshotId",
                table: "LearningAssessmentDraftCriteria",
                column: "CriterionSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisionCriteria_CriterionSnapshotId",
                table: "LearningAssessmentRevisionCriteria",
                column: "CriterionSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisionCriteria_RevisionId_CriterionSnap~",
                table: "LearningAssessmentRevisionCriteria",
                columns: new[] { "RevisionId", "CriterionSnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisions_AssessmentId_FinalizedAt",
                table: "LearningAssessmentRevisions",
                columns: new[] { "AssessmentId", "FinalizedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisions_PlacementId_Stage",
                table: "LearningAssessmentRevisions",
                columns: new[] { "PlacementId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisions_SnapshotId",
                table: "LearningAssessmentRevisions",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessments_LatestFinalizedRevisionId",
                table: "LearningAssessments",
                column: "LatestFinalizedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessments_PlacementId_Stage",
                table: "LearningAssessments",
                columns: new[] { "PlacementId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessments_SnapshotId",
                table: "LearningAssessments",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessments_TenantId_Status",
                table: "LearningAssessments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecordTemplateCriteria_TemplateId_SortOrder",
                table: "LearningRecordTemplateCriteria",
                columns: new[] { "TemplateId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecordTemplates_CompanyId",
                table: "LearningRecordTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecordTemplates_TenantId_CompanyId_Status_Version",
                table: "LearningRecordTemplates",
                columns: new[] { "TenantId", "CompanyId", "Status", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementLearningRecordCriterionSnapshots_SnapshotId_SortOr~",
                table: "PlacementLearningRecordCriterionSnapshots",
                columns: new[] { "SnapshotId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlacementLearningRecordSnapshots_CompanyId",
                table: "PlacementLearningRecordSnapshots",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementLearningRecordSnapshots_PlacementId",
                table: "PlacementLearningRecordSnapshots",
                column: "PlacementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlacementLearningRecordSnapshots_SourceTemplateId",
                table: "PlacementLearningRecordSnapshots",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementLearningRecordSnapshots_TenantId_CompanyId",
                table: "PlacementLearningRecordSnapshots",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMonitoringEvents_FollowUpVisitId",
                table: "TeacherMonitoringEvents",
                column: "FollowUpVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMonitoringEvents_PlacementId",
                table: "TeacherMonitoringEvents",
                column: "PlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMonitoringEvents_TeacherUserId_CreatedAt",
                table: "TeacherMonitoringEvents",
                columns: new[] { "TeacherUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMonitoringEvents_TenantId_PlacementId_Status",
                table: "TeacherMonitoringEvents",
                columns: new[] { "TenantId", "PlacementId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_LearningAssessmentCriterionEvidence_LearningAssessmentDraft~",
                table: "LearningAssessmentCriterionEvidence",
                column: "DraftCriterionId",
                principalTable: "LearningAssessmentDraftCriteria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningAssessmentDraftCriteria_LearningAssessments_Assessm~",
                table: "LearningAssessmentDraftCriteria",
                column: "AssessmentId",
                principalTable: "LearningAssessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningAssessmentRevisionCriteria_LearningAssessmentRevisi~",
                table: "LearningAssessmentRevisionCriteria",
                column: "RevisionId",
                principalTable: "LearningAssessmentRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningAssessmentRevisions_LearningAssessments_AssessmentId",
                table: "LearningAssessmentRevisions",
                column: "AssessmentId",
                principalTable: "LearningAssessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningAssessmentRevisions_LearningAssessments_AssessmentId",
                table: "LearningAssessmentRevisions");

            migrationBuilder.DropTable(
                name: "AssessmentReminderDeliveries");

            migrationBuilder.DropTable(
                name: "LearningAssessmentCriterionEvidence");

            migrationBuilder.DropTable(
                name: "LearningAssessmentRevisionCriteria");

            migrationBuilder.DropTable(
                name: "LearningRecordTemplateCriteria");

            migrationBuilder.DropTable(
                name: "TeacherMonitoringEvents");

            migrationBuilder.DropTable(
                name: "LearningAssessmentDraftCriteria");

            migrationBuilder.DropTable(
                name: "PlacementLearningRecordCriterionSnapshots");

            migrationBuilder.DropTable(
                name: "LearningAssessments");

            migrationBuilder.DropTable(
                name: "LearningAssessmentRevisions");

            migrationBuilder.DropTable(
                name: "PlacementLearningRecordSnapshots");

            migrationBuilder.DropTable(
                name: "LearningRecordTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Placements_TenantId_StudentId",
                table: "Placements");
        }
    }
}
