using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class offer_letter_template_date : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderContact",
                table: "LetterTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HeaderSubtitle",
                table: "LetterTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HeaderTitle",
                table: "LetterTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderContact",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderSubtitle",
                table: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderTitle",
                table: "LetterTemplates");
        }
    }
}
