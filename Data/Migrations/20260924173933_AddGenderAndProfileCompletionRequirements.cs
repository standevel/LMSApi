using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenderAndProfileCompletionRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "JambRegNumber",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfileCompletionRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCompletionRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                        column: x => x.AcademicLevelId,
                        principalTable: "Levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCompletionRequirements_AcademicLevelId",
                table: "ProfileCompletionRequirements",
                column: "AcademicLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCompletionRequirements_AcademicProgramId",
                table: "ProfileCompletionRequirements",
                column: "AcademicProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCompletionRequirements_AcademicSessionId_AcademicLevelId_AcademicProgramId_IsActive",
                table: "ProfileCompletionRequirements",
                columns: new[] { "AcademicSessionId", "AcademicLevelId", "AcademicProgramId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCompletionRequirements_FacultyId",
                table: "ProfileCompletionRequirements",
                column: "FacultyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileCompletionRequirements");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AdmissionApplications");

            migrationBuilder.AlterColumn<string>(
                name: "JambRegNumber",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
