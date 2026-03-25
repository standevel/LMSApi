using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumEnhancements_Final : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationYears",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCurriculumId",
                table: "Curricula",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Curricula",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CoursePrerequisites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrerequisiteCourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePrerequisites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_Courses_PrerequisiteCourseId",
                        column: x => x.PrerequisiteCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LevelSemesterConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    MaxCreditLoad = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelSemesterConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevelSemesterConfigs_Levels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "Levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrerequisites_CourseId_PrerequisiteCourseId",
                table: "CoursePrerequisites",
                columns: new[] { "CourseId", "PrerequisiteCourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrerequisites_PrerequisiteCourseId",
                table: "CoursePrerequisites",
                column: "PrerequisiteCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelSemesterConfigs_LevelId_Semester",
                table: "LevelSemesterConfigs",
                columns: new[] { "LevelId", "Semester" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePrerequisites");

            migrationBuilder.DropTable(
                name: "LevelSemesterConfigs");

            migrationBuilder.DropColumn(
                name: "DurationYears",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "ParentCurriculumId",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Curricula");
        }
    }
}
