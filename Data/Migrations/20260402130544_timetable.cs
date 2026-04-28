using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class timetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LectureTimetableSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureTimetableSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureTimetableSlots_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureTimetableSlots_Subjects_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Subjects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LectureTimetableSlots_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureTimetableSlots_Users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LectureTimetableSlots_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LectureTimetableSlots_CourseOfferingId",
                table: "LectureTimetableSlots",
                column: "CourseOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureTimetableSlots_CreatedByUserId",
                table: "LectureTimetableSlots",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureTimetableSlots_LecturerId",
                table: "LectureTimetableSlots",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureTimetableSlots_UpdatedByUserId",
                table: "LectureTimetableSlots",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureTimetableSlots_VenueId",
                table: "LectureTimetableSlots",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LectureTimetableSlots");
        }
    }
}
