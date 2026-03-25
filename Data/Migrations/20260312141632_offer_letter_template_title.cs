using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class offer_letter_template_title : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderDate",
                table: "LetterTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderDate",
                table: "LetterTemplates");
        }
    }
}
