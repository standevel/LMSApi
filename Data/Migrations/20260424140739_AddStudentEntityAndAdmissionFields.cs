using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEntityAndAdmissionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountCreatedAt",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntraObjectId",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferExpiresAt",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialEmail",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntraObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OfficialEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PersonalEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrollmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GraduationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_AdmissionApplications_AdmissionApplicationId",
                        column: x => x.AdmissionApplicationId,
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Students_Programs_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_AcademicProgramId",
                table: "Students",
                column: "AcademicProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_AcademicSessionId",
                table: "Students",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_AdmissionApplicationId",
                table: "Students",
                column: "AdmissionApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_EntraObjectId",
                table: "Students",
                column: "EntraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_FacultyId",
                table: "Students",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_OfficialEmail",
                table: "Students",
                column: "OfficialEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_Status",
                table: "Students",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentNumber",
                table: "Students",
                column: "StudentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropColumn(
                name: "AccountCreatedAt",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EntraObjectId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "OfferExpiresAt",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "OfficialEmail",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "AdmissionApplications");
        }
    }
}
