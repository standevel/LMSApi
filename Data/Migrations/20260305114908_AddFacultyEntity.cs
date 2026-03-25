using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFacultyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Faculties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faculties", x => x.Id);
                });

            // Seed a default faculty to satisfy foreign key constraint for existing data
            var defaultFacultyId = Guid.NewGuid();
            migrationBuilder.InsertData(
                table: "Faculties",
                columns: new[] { "Id", "Name", "Label" },
                values: new object[] { defaultFacultyId, "General Faculty", "Faculty" });

            migrationBuilder.AddColumn<Guid>(
                name: "FacultyId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultFacultyId);

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Programs");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FacultyId",
                table: "Programs",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_Name",
                table: "Faculties",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Faculties_FacultyId",
                table: "Programs",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Faculties_FacultyId",
                table: "Programs");

            migrationBuilder.DropTable(
                name: "Faculties");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FacultyId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "Programs");

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Programs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
