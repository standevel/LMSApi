using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class application : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmissionOpen",
                table: "AcademicSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdmissionApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    JambRegNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Persona = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmergencyContactJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SponsorshipJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_Programs_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCompulsory = table.Column<bool>(type: "bit", nullable: false),
                    DefaultAccessRules = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccessMetadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRecords_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRecords_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentRecords_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionApplicationDocuments",
                columns: table => new
                {
                    AdmissionApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplicationDocuments", x => new { x.AdmissionApplicationId, x.DocumentsId });
                    table.ForeignKey(
                        name: "FK_AdmissionApplicationDocuments_AdmissionApplications_AdmissionApplicationId",
                        column: x => x.AdmissionApplicationId,
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdmissionApplicationDocuments_DocumentRecords_DocumentsId",
                        column: x => x.DocumentsId,
                        principalTable: "DocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplicationDocuments_DocumentsId",
                table: "AdmissionApplicationDocuments",
                column: "DocumentsId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_AcademicProgramId",
                table: "AdmissionApplications",
                column: "AcademicProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_AcademicSessionId",
                table: "AdmissionApplications",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_FacultyId",
                table: "AdmissionApplications",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_JambRegNumber_AcademicSessionId",
                table: "AdmissionApplications",
                columns: new[] { "JambRegNumber", "AcademicSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_StudentEmail_AcademicSessionId",
                table: "AdmissionApplications",
                columns: new[] { "StudentEmail", "AcademicSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRecords_DocumentTypeId",
                table: "DocumentRecords",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRecords_FacultyId",
                table: "DocumentRecords",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRecords_OwnerId",
                table: "DocumentRecords",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionApplicationDocuments");

            migrationBuilder.DropTable(
                name: "AdmissionApplications");

            migrationBuilder.DropTable(
                name: "DocumentRecords");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "IsAdmissionOpen",
                table: "AcademicSessions");
        }
    }
}
