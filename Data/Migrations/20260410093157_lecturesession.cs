using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class lecturesession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LectureSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimetableSlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsManuallyCreated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureSessions_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LectureSessions_LectureTimetableSlots_TimetableSlotId",
                        column: x => x.TimetableSlotId,
                        principalTable: "LectureTimetableSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LectureSessions_Subjects_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LectureSessions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LectureSessionLecturers",
                columns: table => new
                {
                    LectureSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureSessionLecturers", x => new { x.LectureSessionId, x.LecturerId });
                    table.ForeignKey(
                        name: "FK_LectureSessionLecturers_LectureSessions_LectureSessionId",
                        column: x => x.LectureSessionId,
                        principalTable: "LectureSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureSessionLecturers_Users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessionLecturers_LecturerId",
                table: "LectureSessionLecturers",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessions_CourseOfferingId",
                table: "LectureSessions",
                column: "CourseOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessions_CreatedBy",
                table: "LectureSessions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessions_SessionDate",
                table: "LectureSessions",
                column: "SessionDate");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessions_TimetableSlotId",
                table: "LectureSessions",
                column: "TimetableSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSessions_VenueId",
                table: "LectureSessions",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LectureSessionLecturers");

            migrationBuilder.DropTable(
                name: "LectureSessions");
        }
    }
}
