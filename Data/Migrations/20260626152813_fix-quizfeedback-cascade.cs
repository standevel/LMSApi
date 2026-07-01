using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixQuizFeedbackCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizFeedbacks_Quizzes_QuizId",
                table: "QuizFeedbacks");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizFeedbacks_Quizzes_QuizId",
                table: "QuizFeedbacks",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizFeedbacks_Quizzes_QuizId",
                table: "QuizFeedbacks");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizFeedbacks_Quizzes_QuizId",
                table: "QuizFeedbacks",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
