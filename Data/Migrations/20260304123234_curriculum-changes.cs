using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class curriculumchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicLevelId",
                table: "LevelSemesterConfigs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LevelSemesterConfigs_AcademicLevelId",
                table: "LevelSemesterConfigs",
                column: "AcademicLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_LevelSemesterConfigs_Levels_AcademicLevelId",
                table: "LevelSemesterConfigs",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LevelSemesterConfigs_Levels_AcademicLevelId",
                table: "LevelSemesterConfigs");

            migrationBuilder.DropIndex(
                name: "IX_LevelSemesterConfigs_AcademicLevelId",
                table: "LevelSemesterConfigs");

            migrationBuilder.DropColumn(
                name: "AcademicLevelId",
                table: "LevelSemesterConfigs");
        }
    }
}
