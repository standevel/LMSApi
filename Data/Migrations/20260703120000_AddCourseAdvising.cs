using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    public partial class AddCourseAdvising : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.ForeignKey("FK_CourseAdviserAssignments_Students_StudentId", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_CourseAdviserAssignments_Users_AdviserId", x => x.AdviserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_CourseAdviserAssignments_Users_AssignedById", x => x.AssignedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                });

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
                    table.ForeignKey("FK_AdvisingNotes_Students_StudentId", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_AdvisingNotes_Users_AdviserId", x => x.AdviserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey("FK_RegistrationVerifications_AcademicSessions_AcademicSessionId", x => x.AcademicSessionId, "AcademicSessions", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_RegistrationVerifications_Students_StudentId", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_RegistrationVerifications_Users_UnlockedById", x => x.UnlockedById, "Users", "Id", onDelete: ReferentialAction.NoAction);
                    table.ForeignKey("FK_RegistrationVerifications_Users_VerifiedByAdviserId", x => x.VerifiedByAdviserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_CourseAdviserAssignments_AdviserId_Status", "CourseAdviserAssignments", new[] { "AdviserId", "Status" });
            migrationBuilder.CreateIndex("IX_CourseAdviserAssignments_AssignedById", "CourseAdviserAssignments", "AssignedById");
            migrationBuilder.CreateIndex("IX_CourseAdviserAssignments_Source", "CourseAdviserAssignments", "Source");
            migrationBuilder.CreateIndex("IX_CourseAdviserAssignments_StudentId_Status", "CourseAdviserAssignments", new[] { "StudentId", "Status" }, unique: true, filter: "[Status] = 'Active'");
            migrationBuilder.CreateIndex("IX_AdvisingNotes_AdviserId", "AdvisingNotes", "AdviserId");
            migrationBuilder.CreateIndex("IX_AdvisingNotes_FollowUpDateUtc", "AdvisingNotes", "FollowUpDateUtc");
            migrationBuilder.CreateIndex("IX_AdvisingNotes_StudentId", "AdvisingNotes", "StudentId");
            migrationBuilder.CreateIndex("IX_RegistrationVerifications_AcademicSessionId", "RegistrationVerifications", "AcademicSessionId");
            migrationBuilder.CreateIndex("IX_RegistrationVerifications_StudentId_AcademicSessionId_Status", "RegistrationVerifications", new[] { "StudentId", "AcademicSessionId", "Status" }, unique: true, filter: "[Status] = 'Verified'");
            migrationBuilder.CreateIndex("IX_RegistrationVerifications_UnlockedById", "RegistrationVerifications", "UnlockedById");
            migrationBuilder.CreateIndex("IX_RegistrationVerifications_VerifiedByAdviserId", "RegistrationVerifications", "VerifiedByAdviserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AdvisingNotes");
            migrationBuilder.DropTable(name: "CourseAdviserAssignments");
            migrationBuilder.DropTable(name: "RegistrationVerifications");
        }
    }
}
