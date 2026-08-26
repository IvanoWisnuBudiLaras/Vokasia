using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningRecordExportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExportQuantity",
                table: "ExportRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportScope",
                table: "ExportRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportKind",
                table: "ExportRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportQueryJson",
                table: "ExportRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportQuantity",
                table: "ExportRequests");

            migrationBuilder.DropColumn(
                name: "ExportScope",
                table: "ExportRequests");

            migrationBuilder.DropColumn(
                name: "ReportKind",
                table: "ExportRequests");

            migrationBuilder.DropColumn(
                name: "ReportQueryJson",
                table: "ExportRequests");
        }
    }
}
