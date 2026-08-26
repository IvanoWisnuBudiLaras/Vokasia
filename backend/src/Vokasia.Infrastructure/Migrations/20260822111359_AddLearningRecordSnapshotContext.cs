using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningRecordSnapshotContext : Migration
    {
        public const string SnapshotContextBackfillSql = """
            UPDATE "PlacementLearningRecordSnapshots" AS snapshot
            SET "CompanyDisplayName" = COALESCE(snapshot."CompanyDisplayName", company."Name"),
                "PeriodDisplayName" = COALESCE(snapshot."PeriodDisplayName", period."Name"),
                "PeriodStartDate" = COALESCE(snapshot."PeriodStartDate", period."StartDate"),
                "PeriodEndDate" = COALESCE(snapshot."PeriodEndDate", period."EndDate")
            FROM "Placements" AS placement, "Companies" AS company, "Periods" AS period
            WHERE placement."Id" = snapshot."PlacementId"
              AND company."Id" = snapshot."CompanyId"
              AND period."Id" = placement."PeriodId";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyDisplayName",
                table: "PlacementLearningRecordSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodDisplayName",
                table: "PlacementLearningRecordSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEndDate",
                table: "PlacementLearningRecordSnapshots",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStartDate",
                table: "PlacementLearningRecordSnapshots",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(SnapshotContextBackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyDisplayName",
                table: "PlacementLearningRecordSnapshots");

            migrationBuilder.DropColumn(
                name: "PeriodDisplayName",
                table: "PlacementLearningRecordSnapshots");

            migrationBuilder.DropColumn(
                name: "PeriodEndDate",
                table: "PlacementLearningRecordSnapshots");

            migrationBuilder.DropColumn(
                name: "PeriodStartDate",
                table: "PlacementLearningRecordSnapshots");
        }
    }
}
