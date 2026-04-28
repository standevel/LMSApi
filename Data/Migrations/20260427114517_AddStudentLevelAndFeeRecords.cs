using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentLevelAndFeeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentFeeRecords_Users_StudentId",
                table: "StudentFeeRecords");

            migrationBuilder.AddColumn<Guid>(
                name: "LevelId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_LevelId",
                table: "Students",
                column: "LevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentFeeRecords_Students_StudentId",
                table: "StudentFeeRecords",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Levels_LevelId",
                table: "Students",
                column: "LevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentFeeRecords_Students_StudentId",
                table: "StudentFeeRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Levels_LevelId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_LevelId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "Students");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentFeeRecords_Users_StudentId",
                table: "StudentFeeRecords",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
