using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRubricVersioningAndAssessmentComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RubricTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "RubricTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RubricTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RubricTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE \"RubricTemplates\" SET \"IsActive\" = TRUE, \"Version\" = 1");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RubricAspects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "RubricAspects",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "AssessmentScores",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RubricTemplates_TenantId_CompanyId_IsActive_Version",
                table: "RubricTemplates",
                columns: new[] { "TenantId", "CompanyId", "IsActive", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RubricTemplates_TenantId_CompanyId_IsActive_Version",
                table: "RubricTemplates");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "RubricTemplates");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RubricTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RubricTemplates");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "RubricAspects");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "AssessmentScores");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RubricTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RubricAspects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
