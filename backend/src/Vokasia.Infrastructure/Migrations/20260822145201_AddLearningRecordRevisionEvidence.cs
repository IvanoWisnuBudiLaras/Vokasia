using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningRecordRevisionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningAssessmentRevisionCriterionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentRevisionCriterionEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentRevisionCriterionEvidence_JournalEntries_~",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentRevisionCriterionEvidence_LearningAssessm~",
                        column: x => x.RevisionCriterionId,
                        principalTable: "LearningAssessmentRevisionCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisionCriterionEvidence_JournalEntryId",
                table: "LearningAssessmentRevisionCriterionEvidence",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentRevisionCriterionEvidence_RevisionCriteri~",
                table: "LearningAssessmentRevisionCriterionEvidence",
                column: "RevisionCriterionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningAssessmentRevisionCriterionEvidence");
        }
    }
}
