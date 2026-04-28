using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class timetable_slot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Subjects_VenueId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_CreatedByUserId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_LecturerId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_UpdatedByUserId",
                table: "LectureTimetableSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Subjects_VenueId",
                table: "LectureTimetableSlots",
                column: "VenueId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_CreatedByUserId",
                table: "LectureTimetableSlots",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_LecturerId",
                table: "LectureTimetableSlots",
                column: "LecturerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_UpdatedByUserId",
                table: "LectureTimetableSlots",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Subjects_VenueId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_CreatedByUserId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_LecturerId",
                table: "LectureTimetableSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_LectureTimetableSlots_Users_UpdatedByUserId",
                table: "LectureTimetableSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Subjects_VenueId",
                table: "LectureTimetableSlots",
                column: "VenueId",
                principalTable: "Subjects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_CreatedByUserId",
                table: "LectureTimetableSlots",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_LecturerId",
                table: "LectureTimetableSlots",
                column: "LecturerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LectureTimetableSlots_Users_UpdatedByUserId",
                table: "LectureTimetableSlots",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
