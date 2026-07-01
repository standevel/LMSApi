using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizAssessmentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentCategoryId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_AssessmentCategoryId",
                table: "Quizzes",
                column: "AssessmentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_AssessmentCategories_AssessmentCategoryId",
                table: "Quizzes",
                column: "AssessmentCategoryId",
                principalTable: "AssessmentCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_AssessmentCategories_AssessmentCategoryId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_AssessmentCategoryId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "AssessmentCategoryId",
                table: "Quizzes");
        }
    }
}
