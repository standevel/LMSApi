using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentDirectEntryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirectEntry",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntryQualification",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntryInstitution",
                table: "Students",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DirectEntryPoints",
                table: "Students",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_IsDirectEntry",
                table: "Students",
                column: "IsDirectEntry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_IsDirectEntry",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsDirectEntry",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DirectEntryQualification",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DirectEntryInstitution",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DirectEntryPoints",
                table: "Students");
        }
    }
}
