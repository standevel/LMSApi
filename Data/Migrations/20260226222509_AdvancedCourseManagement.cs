using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdvancedCourseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Levels_LevelId",
                table: "Courses");

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Programs_ProgramId",
                table: "Courses");

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Users_LecturerId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_LevelId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_ProgramId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "LecturerId",
                table: "Courses",
                newName: "AcademicProgramId");

            migrationBuilder.RenameIndex(
                name: "IX_Courses_LecturerId",
                table: "Courses",
                newName: "IX_Courses_AcademicProgramId");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicLevelId",
                table: "Courses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseOfferings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Semester = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Levels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "Levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_AcademicLevelId",
                table: "Courses",
                column: "AcademicLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_AcademicSessionId",
                table: "CourseOfferings",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_ProgramId_LevelId_AcademicSessionId_Semester",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "ProgramId", "LevelId", "AcademicSessionId", "Semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_LecturerId",
                table: "CourseOfferings",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_LevelId",
                table: "CourseOfferings",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_ProgramId",
                table: "CourseOfferings",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Levels_AcademicLevelId",
                table: "Courses",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Programs_AcademicProgramId",
                table: "Courses",
                column: "AcademicProgramId",
                principalTable: "Programs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Levels_AcademicLevelId",
                table: "Courses");

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Programs_AcademicProgramId",
                table: "Courses");

            migrationBuilder.DropTable(
                name: "CourseOfferings");

            migrationBuilder.DropIndex(
                name: "IX_Courses_AcademicLevelId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "AcademicLevelId",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "AcademicProgramId",
                table: "Courses",
                newName: "LecturerId");

            migrationBuilder.RenameIndex(
                name: "IX_Courses_AcademicProgramId",
                table: "Courses",
                newName: "IX_Courses_LecturerId");

            migrationBuilder.AddColumn<Guid>(
                name: "LevelId",
                table: "Courses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "Courses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Courses_LevelId",
                table: "Courses",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ProgramId",
                table: "Courses",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Levels_LevelId",
                table: "Courses",
                column: "LevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Programs_ProgramId",
                table: "Courses",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Users_LecturerId",
                table: "Courses",
                column: "LecturerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
