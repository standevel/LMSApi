using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnableTurnitinCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Users', 'ThemePreference') IS NOT NULL
                BEGIN
                    ALTER TABLE [Users] ALTER COLUMN [ThemePreference] nvarchar(50) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('AssignmentSubmissions', 'TurnitinCheckedAt') IS NULL
                BEGIN
                    ALTER TABLE [AssignmentSubmissions] ADD [TurnitinCheckedAt] datetimeoffset NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('AssignmentSubmissions', 'TurnitinReportUrl') IS NULL
                BEGIN
                    ALTER TABLE [AssignmentSubmissions] ADD [TurnitinReportUrl] nvarchar(512) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('AssignmentSubmissions', 'TurnitinResultJson') IS NULL
                BEGIN
                    ALTER TABLE [AssignmentSubmissions] ADD [TurnitinResultJson] nvarchar(max) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('AssignmentSubmissions', 'TurnitinSimilarityScore') IS NULL
                BEGIN
                    ALTER TABLE [AssignmentSubmissions] ADD [TurnitinSimilarityScore] int NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('AssignmentSubmissions', 'TurnitinStatus') IS NULL
                BEGIN
                    ALTER TABLE [AssignmentSubmissions] ADD [TurnitinStatus] nvarchar(50) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Assignments', 'EnableTurnitinCheck') IS NULL
                BEGIN
                    ALTER TABLE [Assignments] ADD [EnableTurnitinCheck] bit NOT NULL DEFAULT 1;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CourseDocumentChunks]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CourseDocumentChunks] (
                        [Id] uniqueidentifier NOT NULL,
                        [CourseId] uniqueidentifier NOT NULL,
                        [CourseCode] nvarchar(max) NOT NULL,
                        [DocumentTitle] nvarchar(max) NOT NULL,
                        [ChunkText] nvarchar(max) NOT NULL,
                        [PageOrSlideNumber] int NULL,
                        [EmbeddingVectorJson] nvarchar(max) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_CourseDocumentChunks] PRIMARY KEY ([Id])
                    );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseDocumentChunks");

            migrationBuilder.DropColumn(
                name: "TurnitinCheckedAt",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "TurnitinReportUrl",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "TurnitinResultJson",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "TurnitinSimilarityScore",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "TurnitinStatus",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "EnableTurnitinCheck",
                table: "Assignments");

            migrationBuilder.AlterColumn<string>(
                name: "ThemePreference",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
