using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCriteriaToAcademicProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAdmissions",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinJambScore",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequiredJambSubjectsJson",
                table: "Programs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredOLevelSubjectsJson",
                table: "Programs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAdmissions",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "MinJambScore",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "RequiredJambSubjectsJson",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "RequiredOLevelSubjectsJson",
                table: "Programs");
        }
    }
}
