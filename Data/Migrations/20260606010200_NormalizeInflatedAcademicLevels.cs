using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606010200_NormalizeInflatedAcademicLevels")]
    public partial class NormalizeInflatedAcademicLevels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('tempdb..#LevelFix') IS NOT NULL DROP TABLE #LevelFix;

                SELECT
                    l.[Id] AS BadId,
                    l.[ProgramId],
                    CASE
                        WHEN parsed.LevelNumber BETWEEN 1000 AND 5000
                            AND parsed.LevelNumber % 1000 = 0 THEN parsed.LevelNumber / 10
                        WHEN parsed.LevelNumber BETWEEN 10000 AND 50000
                            AND parsed.LevelNumber % 10000 = 0 THEN parsed.LevelNumber / 100
                        ELSE NULL
                    END AS CanonicalNumber
                INTO #LevelFix
                FROM [Levels] l
                CROSS APPLY (
                    SELECT TRY_CONVERT(int, LEFT(l.[Name], CHARINDEX(' ', l.[Name] + ' ') - 1)) AS LevelNumber
                ) parsed
                WHERE parsed.LevelNumber IS NOT NULL
                    AND (
                        (parsed.LevelNumber BETWEEN 1000 AND 5000 AND parsed.LevelNumber % 1000 = 0)
                        OR (parsed.LevelNumber BETWEEN 10000 AND 50000 AND parsed.LevelNumber % 10000 = 0)
                    );

                DELETE FROM #LevelFix
                WHERE CanonicalNumber NOT IN (100, 200, 300, 400, 500);

                ALTER TABLE #LevelFix ADD CanonicalName nvarchar(100) NULL;
                ALTER TABLE #LevelFix ADD CanonicalOrder int NULL;
                ALTER TABLE #LevelFix ADD CanonicalId uniqueidentifier NULL;

                UPDATE #LevelFix
                SET CanonicalName = CONCAT(CanonicalNumber, ' Level'),
                    CanonicalOrder = CanonicalNumber / 100;

                INSERT INTO [Levels] ([Id], [ProgramId], [Name], [Order])
                SELECT NEWID(), f.[ProgramId], f.[CanonicalName], f.[CanonicalOrder]
                FROM #LevelFix f
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [Levels] existing
                    WHERE existing.[ProgramId] = f.[ProgramId]
                        AND existing.[Name] = f.[CanonicalName]
                )
                GROUP BY f.[ProgramId], f.[CanonicalName], f.[CanonicalOrder];

                UPDATE f
                SET CanonicalId = l.[Id]
                FROM #LevelFix f
                INNER JOIN [Levels] l
                    ON l.[ProgramId] = f.[ProgramId]
                    AND l.[Name] = f.[CanonicalName];

                UPDATE cc
                SET [LevelId] = f.[CanonicalId]
                FROM [CurriculumCourses] cc
                INNER JOIN #LevelFix f ON cc.[LevelId] = f.[BadId];

                UPDATE co
                SET [LevelId] = f.[CanonicalId]
                FROM [CourseOfferings] co
                INNER JOIN #LevelFix f ON co.[LevelId] = f.[BadId];

                IF COL_LENGTH('Courses', 'LevelId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE c
                        SET [LevelId] = f.[CanonicalId]
                        FROM [Courses] c
                        INNER JOIN #LevelFix f ON c.[LevelId] = f.[BadId];
                    ');
                END

                IF COL_LENGTH('Students', 'LevelId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE s
                        SET [LevelId] = f.[CanonicalId]
                        FROM [Students] s
                        INNER JOIN #LevelFix f ON s.[LevelId] = f.[BadId];
                    ');
                END

                UPDATE e
                SET [LevelId] = f.[CanonicalId]
                FROM [Enrollments] e
                INNER JOIN #LevelFix f ON e.[LevelId] = f.[BadId];

                IF COL_LENGTH('AdmissionApplications', 'StartingLevelId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE a
                        SET [StartingLevelId] = f.[CanonicalId]
                        FROM [AdmissionApplications] a
                        INNER JOIN #LevelFix f ON a.[StartingLevelId] = f.[BadId];
                    ');
                END

                DELETE badConfig
                FROM [LevelSemesterConfigs] badConfig
                INNER JOIN #LevelFix f ON badConfig.[LevelId] = f.[BadId]
                WHERE EXISTS (
                    SELECT 1
                    FROM [LevelSemesterConfigs] canonicalConfig
                    WHERE canonicalConfig.[LevelId] = f.[CanonicalId]
                        AND canonicalConfig.[Semester] = badConfig.[Semester]
                );

                UPDATE config
                SET [LevelId] = f.[CanonicalId]
                FROM [LevelSemesterConfigs] config
                INNER JOIN #LevelFix f ON config.[LevelId] = f.[BadId];

                DELETE l
                FROM [Levels] l
                INNER JOIN #LevelFix f ON l.[Id] = f.[BadId];
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
