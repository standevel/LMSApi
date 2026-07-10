using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSemesterToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CertificateRequests_Users_StudentId",
                table: "CertificateRequests");

            migrationBuilder.AddColumn<int>(
                name: "ActiveSemester",
                table: "AcademicSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificateRequests_Students_StudentId",
                table: "CertificateRequests",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CertificateRequests_Students_StudentId",
                table: "CertificateRequests");

            migrationBuilder.DropColumn(
                name: "ActiveSemester",
                table: "AcademicSessions");

            migrationBuilder.AddForeignKey(
                name: "FK_CertificateRequests_Users_StudentId",
                table: "CertificateRequests",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
