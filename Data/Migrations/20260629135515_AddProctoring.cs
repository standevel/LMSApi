using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProctoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamProctoringSessions_Students_StudentId",
                table: "ExamProctoringSessions");

            migrationBuilder.AddColumn<string>(
                name: "BrowserInfo",
                table: "ExamProctoringSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CameraPermissionGranted",
                table: "ExamProctoringSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FullscreenLossCount",
                table: "ExamProctoringSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "ExamProctoringSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntegrityScore",
                table: "ExamProctoringSessions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsFullscreen",
                table: "ExamProctoringSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScreenResolution",
                table: "ExamProctoringSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfieCaptureUrl",
                table: "ExamProctoringSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TabSwitchCount",
                table: "ExamProctoringSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "ExamProctoringSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProctoringViolations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScreenshotUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProctoringViolations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProctoringViolations_ExamProctoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ExamProctoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProctoringViolations_OccurredAtUtc",
                table: "ProctoringViolations",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProctoringViolations_SessionId",
                table: "ProctoringViolations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProctoringViolations_ViolationType",
                table: "ProctoringViolations",
                column: "ViolationType");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamProctoringSessions_Users_StudentId",
                table: "ExamProctoringSessions",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamProctoringSessions_Users_StudentId",
                table: "ExamProctoringSessions");

            migrationBuilder.DropTable(
                name: "ProctoringViolations");

            migrationBuilder.DropColumn(
                name: "BrowserInfo",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "CameraPermissionGranted",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "FullscreenLossCount",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "IntegrityScore",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "IsFullscreen",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "ScreenResolution",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "SelfieCaptureUrl",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "TabSwitchCount",
                table: "ExamProctoringSessions");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "ExamProctoringSessions");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamProctoringSessions_Students_StudentId",
                table: "ExamProctoringSessions",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
