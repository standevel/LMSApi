using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseEnrollments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DroppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEnrollments_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_CourseOfferingId",
                table: "CourseEnrollments",
                column: "CourseOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_CreatedById",
                table: "CourseEnrollments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_StudentId_CourseOfferingId",
                table: "CourseEnrollments",
                columns: new[] { "StudentId", "CourseOfferingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_StudentId_Status",
                table: "CourseEnrollments",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_UpdatedById",
                table: "CourseEnrollments",
                column: "UpdatedById");

            migrationBuilder.Sql("""
                INSERT INTO CourseEnrollments (Id, StudentId, CourseOfferingId, Status, RegisteredAtUtc, DroppedAtUtc, CreatedById, UpdatedById)
                SELECT NEWID(), e.UserId, co.Id, 'Registered', e.EnrolledAtUtc, NULL, e.UserId, NULL
                FROM Enrollments e
                INNER JOIN CourseOfferings co ON co.ProgramId = e.ProgramId
                    AND co.LevelId = e.LevelId AND co.AcademicSessionId = e.AcademicSessionId
                WHERE NOT EXISTS (
                    SELECT 1 FROM CourseEnrollments ce
                    WHERE ce.StudentId = e.UserId AND ce.CourseOfferingId = co.Id
                );
                DECLARE @created int = @@ROWCOUNT;
                DECLARE @skipped int = (SELECT COUNT(*) FROM Enrollments e WHERE NOT EXISTS (
                    SELECT 1 FROM CourseOfferings co WHERE co.ProgramId = e.ProgramId
                        AND co.LevelId = e.LevelId AND co.AcademicSessionId = e.AcademicSessionId));
                PRINT CONCAT('Course enrollment backfill created ', @created, ' records; programme enrollments without matching offerings: ', @skipped);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseEnrollments");
        }
    }
}
