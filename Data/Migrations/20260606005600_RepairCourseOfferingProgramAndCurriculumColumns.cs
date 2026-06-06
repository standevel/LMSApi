using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606005600_RepairCourseOfferingProgramAndCurriculumColumns")]
    public partial class RepairCourseOfferingProgramAndCurriculumColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Courses', 'ProgramId') IS NULL
                BEGIN
                    ALTER TABLE [Courses] ADD [ProgramId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Courses', 'ProgramId') IS NOT NULL
                   AND COL_LENGTH('Courses', 'AcademicProgramId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE [Courses]
                        SET [ProgramId] = [AcademicProgramId]
                        WHERE [ProgramId] IS NULL;
                    ');
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Courses', 'ProgramId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        DECLARE @FallbackProgramId uniqueidentifier = (
                            SELECT TOP (1) [Id] FROM [Programs] ORDER BY [Name]
                        );

                        UPDATE [Courses]
                        SET [ProgramId] = @FallbackProgramId
                        WHERE [ProgramId] IS NULL
                            AND @FallbackProgramId IS NOT NULL;
                    ');
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Courses', 'ProgramId') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM [Courses] WHERE [ProgramId] IS NULL
                    )
                    BEGIN
                        ALTER TABLE [Courses] ALTER COLUMN [ProgramId] uniqueidentifier NOT NULL;
                    END
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Courses_ProgramId_Code'
                      AND object_id = OBJECT_ID('Courses')
                )
                BEGIN
                    CREATE INDEX [IX_Courses_ProgramId_Code] ON [Courses] ([ProgramId], [Code]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Courses_Programs_ProgramId'
                )
                BEGIN
                    ALTER TABLE [Courses]
                    ADD CONSTRAINT [FK_Courses_Programs_ProgramId]
                    FOREIGN KEY ([ProgramId]) REFERENCES [Programs] ([Id])
                    ON DELETE NO ACTION;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'ProgramId') IS NULL
                BEGIN
                    ALTER TABLE [CourseOfferings] ADD [ProgramId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'CurriculumId') IS NULL
                BEGIN
                    ALTER TABLE [CourseOfferings] ADD [CurriculumId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'ProgramId') IS NOT NULL
                   AND COL_LENGTH('Courses', 'ProgramId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE co
                        SET [ProgramId] = c.[ProgramId]
                        FROM [CourseOfferings] co
                        INNER JOIN [Courses] c ON co.[CourseId] = c.[Id]
                        WHERE co.[ProgramId] IS NULL;
                    ');
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'ProgramId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        DECLARE @FallbackProgramId uniqueidentifier = (
                            SELECT TOP (1) [Id] FROM [Programs] ORDER BY [Name]
                        );

                        UPDATE [CourseOfferings]
                        SET [ProgramId] = @FallbackProgramId
                        WHERE [ProgramId] IS NULL
                            AND @FallbackProgramId IS NOT NULL;
                    ');
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'ProgramId') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM [CourseOfferings] WHERE [ProgramId] IS NULL
                    )
                    BEGIN
                        ALTER TABLE [CourseOfferings] ALTER COLUMN [ProgramId] uniqueidentifier NOT NULL;
                    END
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_CourseOfferings_ProgramId'
                      AND object_id = OBJECT_ID('CourseOfferings')
                )
                BEGIN
                    CREATE INDEX [IX_CourseOfferings_ProgramId] ON [CourseOfferings] ([ProgramId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_CourseOfferings_CurriculumId'
                      AND object_id = OBJECT_ID('CourseOfferings')
                )
                BEGIN
                    CREATE INDEX [IX_CourseOfferings_CurriculumId] ON [CourseOfferings] ([CurriculumId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_CourseOfferings_Programs_ProgramId'
                )
                BEGIN
                    ALTER TABLE [CourseOfferings]
                    ADD CONSTRAINT [FK_CourseOfferings_Programs_ProgramId]
                    FOREIGN KEY ([ProgramId]) REFERENCES [Programs] ([Id])
                    ON DELETE NO ACTION;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_CourseOfferings_Curricula_CurriculumId'
                )
                BEGIN
                    ALTER TABLE [CourseOfferings]
                    ADD CONSTRAINT [FK_CourseOfferings_Curricula_CurriculumId]
                    FOREIGN KEY ([CurriculumId]) REFERENCES [Curricula] ([Id]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Courses_Programs_ProgramId'
                )
                BEGIN
                    ALTER TABLE [Courses]
                    DROP CONSTRAINT [FK_Courses_Programs_ProgramId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Courses_ProgramId_Code'
                      AND object_id = OBJECT_ID('Courses')
                )
                BEGIN
                    DROP INDEX [IX_Courses_ProgramId_Code] ON [Courses];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Courses', 'ProgramId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Courses] DROP COLUMN [ProgramId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_CourseOfferings_Curricula_CurriculumId'
                )
                BEGIN
                    ALTER TABLE [CourseOfferings]
                    DROP CONSTRAINT [FK_CourseOfferings_Curricula_CurriculumId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_CourseOfferings_Programs_ProgramId'
                )
                BEGIN
                    ALTER TABLE [CourseOfferings]
                    DROP CONSTRAINT [FK_CourseOfferings_Programs_ProgramId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_CourseOfferings_CurriculumId'
                      AND object_id = OBJECT_ID('CourseOfferings')
                )
                BEGIN
                    DROP INDEX [IX_CourseOfferings_CurriculumId] ON [CourseOfferings];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_CourseOfferings_ProgramId'
                      AND object_id = OBJECT_ID('CourseOfferings')
                )
                BEGIN
                    DROP INDEX [IX_CourseOfferings_ProgramId] ON [CourseOfferings];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'CurriculumId') IS NOT NULL
                BEGIN
                    ALTER TABLE [CourseOfferings] DROP COLUMN [CurriculumId];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CourseOfferings', 'ProgramId') IS NOT NULL
                BEGIN
                    ALTER TABLE [CourseOfferings] DROP COLUMN [ProgramId];
                END
                """);
        }
    }
}
