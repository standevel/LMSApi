using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourseAdviser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvisingNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdviserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FollowUpDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsStaffOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisingNotes_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisingNotes_Users_AdviserId",
                        column: x => x.AdviserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassterResultUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ProcessedRows = table.Column<int>(type: "int", nullable: false),
                    SuccessfulRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassterResultUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassterResultUploads_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseAdviserAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdviserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAdviserAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAdviserAssignments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAdviserAssignments_Users_AdviserId",
                        column: x => x.AdviserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAdviserAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedByAdviserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UnlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlockedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationVerifications_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrationVerifications_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrationVerifications_Users_UnlockedById",
                        column: x => x.UnlockedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RegistrationVerifications_Users_VerifiedByAdviserId",
                        column: x => x.VerifiedByAdviserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassterResultUploadRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ExternalStudentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssessmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MarksObtained = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MappingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MappingReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassterResultUploadRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassterResultUploadRows_ClassterResultUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "ClassterResultUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisingNotes_AdviserId",
                table: "AdvisingNotes",
                column: "AdviserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisingNotes_FollowUpDateUtc",
                table: "AdvisingNotes",
                column: "FollowUpDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisingNotes_StudentId",
                table: "AdvisingNotes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploadRows_UploadId",
                table: "ClassterResultUploadRows",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploadRows_UploadId_Fingerprint",
                table: "ClassterResultUploadRows",
                columns: new[] { "UploadId", "Fingerprint" },
                unique: true,
                filter: "[Fingerprint] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploadRows_UploadId_RowNumber",
                table: "ClassterResultUploadRows",
                columns: new[] { "UploadId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploads_AcademicSessionId",
                table: "ClassterResultUploads",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploads_CourseId",
                table: "ClassterResultUploads",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploads_CreatedById",
                table: "ClassterResultUploads",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ClassterResultUploads_UploadId",
                table: "ClassterResultUploads",
                column: "UploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdviserAssignments_AdviserId_Status",
                table: "CourseAdviserAssignments",
                columns: new[] { "AdviserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdviserAssignments_AssignedById",
                table: "CourseAdviserAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdviserAssignments_Source",
                table: "CourseAdviserAssignments",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdviserAssignments_StudentId_Status",
                table: "CourseAdviserAssignments",
                columns: new[] { "StudentId", "Status" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationVerifications_AcademicSessionId",
                table: "RegistrationVerifications",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationVerifications_StudentId_AcademicSessionId_Status",
                table: "RegistrationVerifications",
                columns: new[] { "StudentId", "AcademicSessionId", "Status" },
                unique: true,
                filter: "[Status] = 'Verified'");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationVerifications_UnlockedById",
                table: "RegistrationVerifications",
                column: "UnlockedById");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationVerifications_VerifiedByAdviserId",
                table: "RegistrationVerifications",
                column: "VerifiedByAdviserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisingNotes");

            migrationBuilder.DropTable(
                name: "ClassterResultUploadRows");

            migrationBuilder.DropTable(
                name: "CourseAdviserAssignments");

            migrationBuilder.DropTable(
                name: "RegistrationVerifications");

            migrationBuilder.DropTable(
                name: "ClassterResultUploads");
        }
    }
}
