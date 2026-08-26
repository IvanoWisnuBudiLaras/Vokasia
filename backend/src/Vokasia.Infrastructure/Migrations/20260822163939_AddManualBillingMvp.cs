using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBillingMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_PeriodMonth",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "Invoices",
                newName: "AmountSnapshot");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Plans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceAnnual",
                table: "Plans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Plans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"Invoices\" SET \"InvoiceNumber\" = 'VOK-BACKFILL-' || SUBSTRING(CAST(\"Id\" AS text), 1, 8) WHERE \"InvoiceNumber\" = '';");
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PlanNameSnapshot",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentCapacitySnapshot",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PaymentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProofKey = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Approved = table.Column<bool>(type: "boolean", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerificationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanNameSnapshot = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_PeriodMonth",
                table: "Invoices",
                columns: new[] { "TenantId", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_Status",
                table: "Invoices",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSubmissions_InvoiceId_SubmittedAt",
                table: "PaymentSubmissions",
                columns: new[] { "InvoiceId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSubmissions_TenantId_InvoiceId",
                table: "PaymentSubmissions",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_Status",
                table: "Subscriptions",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentSubmissions");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_PeriodMonth",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_Status",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "PriceAnnual",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlanNameSnapshot",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "StudentCapacitySnapshot",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "AmountSnapshot",
                table: "Invoices",
                newName: "Amount");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_PeriodMonth",
                table: "Invoices",
                columns: new[] { "TenantId", "PeriodMonth" },
                unique: true);
        }
    }
}
