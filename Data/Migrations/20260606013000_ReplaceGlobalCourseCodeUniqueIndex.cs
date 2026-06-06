using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606013000_ReplaceGlobalCourseCodeUniqueIndex")]
    public partial class ReplaceGlobalCourseCodeUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_Courses_Code'
                      AND object_id = OBJECT_ID('Courses')
                )
                BEGIN
                    DROP INDEX [IX_Courses_Code] ON [Courses];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_Courses_ProgramId_Code'
                      AND object_id = OBJECT_ID('Courses')
                )
                BEGIN
                    DROP INDEX [IX_Courses_ProgramId_Code] ON [Courses];
                END

                CREATE UNIQUE INDEX [IX_Courses_ProgramId_Code]
                    ON [Courses] ([ProgramId], [Code]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_Courses_ProgramId_Code'
                      AND object_id = OBJECT_ID('Courses')
                )
                BEGIN
                    DROP INDEX [IX_Courses_ProgramId_Code] ON [Courses];
                END

                CREATE UNIQUE INDEX [IX_Courses_Code]
                    ON [Courses] ([Code]);
                """);
        }
    }
}
