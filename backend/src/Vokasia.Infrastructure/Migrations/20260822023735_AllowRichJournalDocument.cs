using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vokasia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowRichJournalDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "JournalEntries",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "JournalEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12000)",
                oldMaxLength: 12000);
        }
    }
}
