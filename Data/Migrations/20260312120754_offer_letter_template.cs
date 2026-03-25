using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class offer_letter_template : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationNumber",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LetterTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LogoBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LetterTemplates_TemplateType",
                table: "LetterTemplates",
                column: "TemplateType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "ApplicationNumber",
                table: "AdmissionApplications");
        }
    }
}
