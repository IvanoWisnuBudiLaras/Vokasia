using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateRevocationAndPortfolioDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftHeadline",
                table: "Portfolios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftSampleJournalIdsCsv",
                table: "Portfolios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InternalRevocationNote",
                table: "Certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicRevocationReason",
                table: "Certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "Certificates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftHeadline",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "DraftSampleJournalIdsCsv",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "InternalRevocationNote",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "PublicRevocationReason",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "Certificates");
        }
    }
}
