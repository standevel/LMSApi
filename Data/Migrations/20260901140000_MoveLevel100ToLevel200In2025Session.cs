using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260901140000_MoveLevel100ToLevel200In2025Session")]
    public partial class MoveLevel100ToLevel200In2025Session : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @SourceSessionId uniqueidentifier = (SELECT TOP 1 [Id] FROM [AcademicSessions] WHERE [Name] = '2024/2025');
                DECLARE @TargetSessionId uniqueidentifier = (SELECT TOP 1 [Id] FROM [AcademicSessions] WHERE [Name] = '2025/2026');

                IF @SourceSessionId IS NULL OR @TargetSessionId IS NULL
                BEGIN
                    RAISERROR('Source session (2024/2025) or target session (2025/2026) not found. Aborting migration.', 16, 1);
                    RETURN;
                END

                -- Build level mapping: Order 1 (100 Level) -> Order 2 (200 Level) per program
                IF OBJECT_ID('tempdb..#LevelMap') IS NOT NULL DROP TABLE #LevelMap;
                SELECT
                    l100.[Id]  AS OldLevelId,
                    l200.[Id]  AS NewLevelId,
                    l100.[ProgramId]
                INTO #LevelMap
                FROM [Levels] l100
                INNER JOIN [Levels] l200
                    ON l200.[ProgramId] = l100.[ProgramId]
                   AND l200.[Order] = 2
                WHERE l100.[Order] = 1;

                IF NOT EXISTS (SELECT 1 FROM #LevelMap)
                BEGIN
                    RETURN;
                END

                -- Capture affected student IDs:
                --   (a) Students currently at level 100 in source session
                --   (b) Students who have a level-100 enrollment in the source session
                --       (their personal AcademicSessionId may differ)
                IF OBJECT_ID('tempdb..#AffectedStudents') IS NOT NULL DROP TABLE #AffectedStudents;
                CREATE TABLE #AffectedStudents (StudentId uniqueidentifier PRIMARY KEY);

                INSERT INTO #AffectedStudents (StudentId)
                SELECT s.[Id]
                FROM [Students] s
                INNER JOIN #LevelMap lm ON s.[LevelId] = lm.OldLevelId
                WHERE s.[AcademicSessionId] = @SourceSessionId;

                INSERT INTO #AffectedStudents (StudentId)
                SELECT DISTINCT e.[UserId]
                FROM [Enrollments] e
                INNER JOIN #LevelMap lm ON e.[LevelId] = lm.OldLevelId
                WHERE e.[AcademicSessionId] = @SourceSessionId
                  AND e.[UserId] NOT IN (SELECT [StudentId] FROM #AffectedStudents);

                IF NOT EXISTS (SELECT 1 FROM #AffectedStudents)
                BEGIN
                    RETURN;
                END

                -- Phase 1: Reassign CourseEnrollments from source offerings to matching target offerings
                UPDATE ce
                SET [CourseOfferingId] = co_target.[Id]
                FROM [CourseEnrollments] ce
                INNER JOIN [CourseOfferings] co_source ON ce.[CourseOfferingId] = co_source.[Id]
                INNER JOIN [CourseOfferings] co_target
                    ON co_target.[CourseId] = co_source.[CourseId]
                   AND co_target.[Semester] = co_source.[Semester]
                   AND co_target.[AcademicSessionId] = @TargetSessionId
                WHERE co_source.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [CourseOfferingPrograms] cop
                      INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId
                      WHERE cop.[CourseOfferingId] = co_source.[Id]
                  );

                -- Phase 2: Delete source CourseOfferingPrograms at level 100 where a matching
                --          target entry already exists at level 200 (same offering + program)
                DELETE cop_source
                FROM [CourseOfferingPrograms] cop_source
                INNER JOIN [CourseOfferings] co_source ON cop_source.[CourseOfferingId] = co_source.[Id]
                INNER JOIN #LevelMap lm ON cop_source.[LevelId] = lm.OldLevelId
                WHERE co_source.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [CourseOfferings] co_target
                      INNER JOIN [CourseOfferingPrograms] cop_target ON co_target.[Id] = cop_target.[CourseOfferingId]
                      INNER JOIN #LevelMap lm2 ON cop_target.[LevelId] = lm2.NewLevelId
                      WHERE co_target.[CourseId] = co_source.[CourseId]
                        AND co_target.[Semester] = co_source.[Semester]
                        AND co_target.[AcademicSessionId] = @TargetSessionId
                        AND cop_target.[ProgramId] = cop_source.[ProgramId]
                  );

                -- Phase 3: Delete source CourseOfferings that have a matching target offering
                --          (safe now since CourseEnrollments have been reassigned and
                --           conflicting CourseOfferingPrograms have been deleted)
                DELETE co
                FROM [CourseOfferings] co
                WHERE co.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [CourseOfferings] co2
                      WHERE co2.[CourseId] = co.[CourseId]
                        AND co2.[Semester] = co.[Semester]
                        AND co2.[AcademicSessionId] = @TargetSessionId
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM [CourseEnrollments] ce
                      WHERE ce.[CourseOfferingId] = co.[Id]
                  );

                -- Phase 4: Move remaining CourseOfferings from source to target session
                UPDATE co
                SET [AcademicSessionId] = @TargetSessionId
                FROM [CourseOfferings] co
                WHERE co.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [CourseOfferingPrograms] cop
                      INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId
                      WHERE cop.[CourseOfferingId] = co.[Id]
                  );

                -- Phase 5: Delete CourseOfferingProgram entries at level 100 that would
                --          conflict after updating to level 200 (same offering+program already at 200)
                DELETE cop
                FROM [CourseOfferingPrograms] cop
                INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId
                WHERE EXISTS (
                    SELECT 1 FROM [CourseOfferingPrograms] cop2
                    INNER JOIN #LevelMap lm2 ON cop2.[LevelId] = lm2.NewLevelId
                    WHERE cop2.[CourseOfferingId] = cop.[CourseOfferingId]
                      AND cop2.[ProgramId] = cop.[ProgramId]
                );

                -- Phase 6: Update remaining CourseOfferingPrograms level 100 -> 200
                UPDATE cop
                SET [LevelId] = lm.[NewLevelId]
                FROM [CourseOfferingPrograms] cop
                INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId;

                -- Phase 7: Delete source Enrollments at level 100 where ANY target enrollment
                --          already exists for the same student (regardless of target level)
                DELETE e_source
                FROM [Enrollments] e_source
                INNER JOIN #LevelMap lm ON e_source.[LevelId] = lm.OldLevelId
                WHERE e_source.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [Enrollments] e_target
                      WHERE e_target.[UserId] = e_source.[UserId]
                        AND e_target.[AcademicSessionId] = @TargetSessionId
                  );

                -- Phase 8: Ensure target Enrollments are at level 200 (update from 100 if needed)
                UPDATE e
                SET [LevelId] = lm.[NewLevelId]
                FROM [Enrollments] e
                INNER JOIN #LevelMap lm ON e.[LevelId] = lm.OldLevelId
                WHERE e.[AcademicSessionId] = @TargetSessionId;

                -- Phase 9: Move remaining source Enrollments to target session + level 200
                --          Safety: skip any that would conflict with existing target enrollments
                UPDATE e
                SET [LevelId] = lm.[NewLevelId],
                    [AcademicSessionId] = @TargetSessionId
                FROM [Enrollments] e
                INNER JOIN #LevelMap lm ON e.[LevelId] = lm.OldLevelId
                WHERE e.[AcademicSessionId] = @SourceSessionId
                  AND NOT EXISTS (
                      SELECT 1 FROM [Enrollments] e_target
                      WHERE e_target.[UserId] = e.[UserId]
                        AND e_target.[AcademicSessionId] = @TargetSessionId
                  );

                -- Phase 10: Update Students: level 100 / session 2024/2025 -> level 200 / session 2025/2026
                --           Only for students whose personal AcademicSessionId is the source session
                UPDATE s
                SET [LevelId] = lm.[NewLevelId],
                    [AcademicSessionId] = @TargetSessionId,
                    [UpdatedAt] = SYSUTCDATETIME()
                FROM [Students] s
                INNER JOIN #LevelMap lm ON s.[LevelId] = lm.OldLevelId
                WHERE s.[AcademicSessionId] = @SourceSessionId;

                -- Phase 11: Delete source StudentFeeRecords where target record already exists
                DELETE fr_source
                FROM [StudentFeeRecords] fr_source
                INNER JOIN #AffectedStudents a ON fr_source.[StudentId] = a.[StudentId]
                WHERE fr_source.[SessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [StudentFeeRecords] fr_target
                      WHERE fr_target.[StudentId] = fr_source.[StudentId]
                        AND fr_target.[SessionId] = @TargetSessionId
                  );

                -- Phase 12: Move remaining StudentFeeRecords to target session
                UPDATE fr
                SET [SessionId] = @TargetSessionId
                FROM [StudentFeeRecords] fr
                INNER JOIN #AffectedStudents a ON fr.[StudentId] = a.[StudentId]
                WHERE fr.[SessionId] = @SourceSessionId;

                -- Phase 13: Delete source FeeAssignments where target assignment already exists
                DELETE fa_source
                FROM [FeeAssignments] fa_source
                INNER JOIN #AffectedStudents a ON fa_source.[StudentId] = a.[StudentId]
                WHERE fa_source.[SessionId] = @SourceSessionId
                  AND fa_source.[StudentId] IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM [FeeAssignments] fa_target
                      WHERE fa_target.[StudentId] = fa_source.[StudentId]
                        AND fa_target.[SessionId] = @TargetSessionId
                  );

                -- Phase 14: Move remaining FeeAssignments to target session
                UPDATE fa
                SET [SessionId] = @TargetSessionId
                FROM [FeeAssignments] fa
                INNER JOIN #AffectedStudents a ON fa.[StudentId] = a.[StudentId]
                WHERE fa.[SessionId] = @SourceSessionId;

                -- Phase 15: Delete source StudentScholarships where target already exists
                DELETE ss_source
                FROM [StudentScholarships] ss_source
                INNER JOIN #AffectedStudents a ON ss_source.[StudentId] = a.[StudentId]
                WHERE ss_source.[SessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [StudentScholarships] ss_target
                      WHERE ss_target.[StudentId] = ss_source.[StudentId]
                        AND ss_target.[ScholarshipId] = ss_source.[ScholarshipId]
                        AND ss_target.[SessionId] = @TargetSessionId
                  );

                -- Phase 16: Move remaining StudentScholarships to target session
                UPDATE ss
                SET [SessionId] = @TargetSessionId
                FROM [StudentScholarships] ss
                INNER JOIN #AffectedStudents a ON ss.[StudentId] = a.[StudentId]
                WHERE ss.[SessionId] = @SourceSessionId;

                -- Phase 17: Delete source AcademicStandings where target already exists
                DELETE ast_source
                FROM [AcademicStandings] ast_source
                INNER JOIN #AffectedStudents a ON ast_source.[StudentId] = a.[StudentId]
                WHERE ast_source.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [AcademicStandings] ast_target
                      WHERE ast_target.[StudentId] = ast_source.[StudentId]
                        AND ast_target.[AcademicSessionId] = @TargetSessionId
                  );

                -- Phase 18: Move remaining AcademicStandings to target session
                UPDATE ast
                SET [AcademicSessionId] = @TargetSessionId
                FROM [AcademicStandings] ast
                INNER JOIN #AffectedStudents a ON ast.[StudentId] = a.[StudentId]
                WHERE ast.[AcademicSessionId] = @SourceSessionId;

                -- Phase 19: Delete source RegistrationVerifications where target already exists
                DELETE rv_source
                FROM [RegistrationVerifications] rv_source
                INNER JOIN #AffectedStudents a ON rv_source.[StudentId] = a.[StudentId]
                WHERE rv_source.[AcademicSessionId] = @SourceSessionId
                  AND EXISTS (
                      SELECT 1 FROM [RegistrationVerifications] rv_target
                      WHERE rv_target.[StudentId] = rv_source.[StudentId]
                        AND rv_target.[AcademicSessionId] = @TargetSessionId
                  );

                -- Phase 20: Move remaining RegistrationVerifications to target session
                UPDATE rv
                SET [AcademicSessionId] = @TargetSessionId
                FROM [RegistrationVerifications] rv
                INNER JOIN #AffectedStudents a ON rv.[StudentId] = a.[StudentId]
                WHERE rv.[AcademicSessionId] = @SourceSessionId;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @SourceSessionId uniqueidentifier = (SELECT TOP 1 [Id] FROM [AcademicSessions] WHERE [Name] = '2024/2025');
                DECLARE @TargetSessionId uniqueidentifier = (SELECT TOP 1 [Id] FROM [AcademicSessions] WHERE [Name] = '2025/2026');

                IF @SourceSessionId IS NULL OR @TargetSessionId IS NULL
                BEGIN
                    RETURN;
                END

                -- Build reverse level mapping: Order 2 (200 Level) -> Order 1 (100 Level) per program
                IF OBJECT_ID('tempdb..#LevelMap') IS NOT NULL DROP TABLE #LevelMap;
                SELECT
                    l200.[Id]  AS OldLevelId,
                    l100.[Id]  AS NewLevelId
                INTO #LevelMap
                FROM [Levels] l200
                INNER JOIN [Levels] l100
                    ON l100.[ProgramId] = l200.[ProgramId]
                   AND l100.[Order] = 1
                WHERE l200.[Order] = 2;

                IF NOT EXISTS (SELECT 1 FROM #LevelMap)
                BEGIN
                    RETURN;
                END

                -- Capture affected student IDs (same logic as Up but for reverse direction)
                IF OBJECT_ID('tempdb..#AffectedStudents') IS NOT NULL DROP TABLE #AffectedStudents;
                CREATE TABLE #AffectedStudents (StudentId uniqueidentifier PRIMARY KEY);

                -- Students currently at level 200 in target session
                INSERT INTO #AffectedStudents (StudentId)
                SELECT s.[Id]
                FROM [Students] s
                INNER JOIN #LevelMap lm ON s.[LevelId] = lm.OldLevelId
                WHERE s.[AcademicSessionId] = @TargetSessionId;

                -- Students with level-200 enrollments in target session
                INSERT INTO #AffectedStudents (StudentId)
                SELECT DISTINCT e.[UserId]
                FROM [Enrollments] e
                INNER JOIN #LevelMap lm ON e.[LevelId] = lm.OldLevelId
                WHERE e.[AcademicSessionId] = @TargetSessionId
                  AND e.[UserId] NOT IN (SELECT [StudentId] FROM #AffectedStudents);

                IF NOT EXISTS (SELECT 1 FROM #AffectedStudents)
                BEGIN
                    RETURN;
                END

                -- Reverse RegistrationVerifications
                DELETE rv_source
                FROM [RegistrationVerifications] rv_source
                INNER JOIN #AffectedStudents a ON rv_source.[StudentId] = a.[StudentId]
                WHERE rv_source.[AcademicSessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [RegistrationVerifications] rv_target
                      WHERE rv_target.[StudentId] = rv_source.[StudentId]
                        AND rv_target.[AcademicSessionId] = @SourceSessionId
                  );
                UPDATE rv
                SET [AcademicSessionId] = @SourceSessionId
                FROM [RegistrationVerifications] rv
                INNER JOIN #AffectedStudents a ON rv.[StudentId] = a.[StudentId]
                WHERE rv.[AcademicSessionId] = @TargetSessionId;

                -- Reverse AcademicStandings
                DELETE ast_source
                FROM [AcademicStandings] ast_source
                INNER JOIN #AffectedStudents a ON ast_source.[StudentId] = a.[StudentId]
                WHERE ast_source.[AcademicSessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [AcademicStandings] ast_target
                      WHERE ast_target.[StudentId] = ast_source.[StudentId]
                        AND ast_target.[AcademicSessionId] = @SourceSessionId
                  );
                UPDATE ast
                SET [AcademicSessionId] = @SourceSessionId
                FROM [AcademicStandings] ast
                INNER JOIN #AffectedStudents a ON ast.[StudentId] = a.[StudentId]
                WHERE ast.[AcademicSessionId] = @TargetSessionId;

                -- Reverse StudentScholarships
                DELETE ss_source
                FROM [StudentScholarships] ss_source
                INNER JOIN #AffectedStudents a ON ss_source.[StudentId] = a.[StudentId]
                WHERE ss_source.[SessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [StudentScholarships] ss_target
                      WHERE ss_target.[StudentId] = ss_source.[StudentId]
                        AND ss_target.[ScholarshipId] = ss_source.[ScholarshipId]
                        AND ss_target.[SessionId] = @SourceSessionId
                  );
                UPDATE ss
                SET [SessionId] = @SourceSessionId
                FROM [StudentScholarships] ss
                INNER JOIN #AffectedStudents a ON ss.[StudentId] = a.[StudentId]
                WHERE ss.[SessionId] = @TargetSessionId;

                -- Reverse FeeAssignments
                DELETE fa_source
                FROM [FeeAssignments] fa_source
                INNER JOIN #AffectedStudents a ON fa_source.[StudentId] = a.[StudentId]
                WHERE fa_source.[SessionId] = @TargetSessionId
                  AND fa_source.[StudentId] IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM [FeeAssignments] fa_target
                      WHERE fa_target.[StudentId] = fa_source.[StudentId]
                        AND fa_target.[SessionId] = @SourceSessionId
                  );
                UPDATE fa
                SET [SessionId] = @SourceSessionId
                FROM [FeeAssignments] fa
                INNER JOIN #AffectedStudents a ON fa.[StudentId] = a.[StudentId]
                WHERE fa.[SessionId] = @TargetSessionId;

                -- Reverse StudentFeeRecords
                DELETE fr_source
                FROM [StudentFeeRecords] fr_source
                INNER JOIN #AffectedStudents a ON fr_source.[StudentId] = a.[StudentId]
                WHERE fr_source.[SessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [StudentFeeRecords] fr_target
                      WHERE fr_target.[StudentId] = fr_source.[StudentId]
                        AND fr_target.[SessionId] = @SourceSessionId
                  );
                UPDATE fr
                SET [SessionId] = @SourceSessionId
                FROM [StudentFeeRecords] fr
                INNER JOIN #AffectedStudents a ON fr.[StudentId] = a.[StudentId]
                WHERE fr.[SessionId] = @TargetSessionId;

                -- Reverse Students
                UPDATE s
                SET [LevelId] = lm.[NewLevelId],
                    [AcademicSessionId] = @SourceSessionId,
                    [UpdatedAt] = SYSUTCDATETIME()
                FROM [Students] s
                INNER JOIN #LevelMap lm ON s.[LevelId] = lm.OldLevelId
                WHERE s.[AcademicSessionId] = @TargetSessionId
                  AND NOT EXISTS (
                      SELECT 1 FROM [Students] s2
                      WHERE s2.[Id] = s.[Id]
                        AND s2.[AcademicSessionId] = @SourceSessionId
                  );

                -- Reverse Enrollments
                DELETE e_source
                FROM [Enrollments] e_source
                INNER JOIN #LevelMap lm ON e_source.[LevelId] = lm.OldLevelId
                INNER JOIN #AffectedStudents a ON e_source.[UserId] = a.[StudentId]
                WHERE e_source.[AcademicSessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [Enrollments] e_target
                      WHERE e_target.[UserId] = e_source.[UserId]
                        AND e_target.[AcademicSessionId] = @SourceSessionId
                  );
                UPDATE e
                SET [LevelId] = lm.[NewLevelId],
                    [AcademicSessionId] = @SourceSessionId
                FROM [Enrollments] e
                INNER JOIN #LevelMap lm ON e.[LevelId] = lm.OldLevelId
                WHERE e.[AcademicSessionId] = @TargetSessionId
                  AND NOT EXISTS (
                      SELECT 1 FROM [Enrollments] e2
                      WHERE e2.[UserId] = e.[UserId]
                        AND e2.[AcademicSessionId] = @SourceSessionId
                  );

                -- Reverse CourseOfferingPrograms: level 200 -> level 100
                UPDATE cop
                SET [LevelId] = lm.[NewLevelId]
                FROM [CourseOfferingPrograms] cop
                INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId;

                -- Reverse CourseOfferings: session 2025/2026 -> 2024/2025
                UPDATE co
                SET [AcademicSessionId] = @SourceSessionId
                FROM [CourseOfferings] co
                WHERE co.[AcademicSessionId] = @TargetSessionId
                  AND EXISTS (
                      SELECT 1 FROM [CourseOfferingPrograms] cop
                      INNER JOIN #LevelMap lm ON cop.[LevelId] = lm.OldLevelId
                      WHERE cop.[CourseOfferingId] = co.[Id]
                  );
            """);
        }
    }
}
