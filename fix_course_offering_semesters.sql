-- ==============================================================================
-- SQL Remediation Script: Fix CourseOfferings Semester Mismatches and Duplicates
-- System: Wigwe University LMS
-- Purpose:
--   Ensures that every CourseOffering in a session belongs ONLY to the semester
--   indicated by its course code parity:
--     - Odd last digit (1, 3, 5, 7, 9)  -> First Semester (Semester = 1) (e.g. WU 211)
--     - Even last digit (0, 2, 4, 6, 8) -> Second Semester (Semester = 2) (e.g. WU GST 212)
--
--   For each course and academic session:
--     1. If both a First Semester and Second Semester offering exist:
--        - The wrong-semester offering is merged into the correct-semester offering
--          (re-linking CourseEnrollments, CourseOfferingPrograms, CourseOfferingLecturers,
--           StudentCourseResults, Assessments, Quizzes, Assignments, LectureTimetableSlots).
--        - The wrong-semester offering is deleted.
--     2. If only a wrong-semester offering exists:
--        - Its Semester is updated to the correct TargetSemester.
-- ==============================================================================

BEGIN TRANSACTION;
BEGIN TRY

    PRINT '>>> Step 1: Mapping courses to target semester based on course code parity...';

    IF OBJECT_ID('tempdb..#CourseRules') IS NOT NULL DROP TABLE #CourseRules;
    CREATE TABLE #CourseRules (
        CourseId UNIQUEIDENTIFIER PRIMARY KEY,
        Code NVARCHAR(100),
        TargetSemester INT
    );

    INSERT INTO #CourseRules (CourseId, Code, TargetSemester)
    SELECT 
        c.Id AS CourseId,
        c.Code,
        CASE 
            WHEN PATINDEX('%[0-9][0-9][0-9]%', c.Code) > 0 THEN
                CASE 
                    WHEN SUBSTRING(c.Code, PATINDEX('%[0-9][0-9][0-9]%', c.Code) + 2, 1) IN ('1', '3', '5', '7', '9') THEN 1
                    WHEN SUBSTRING(c.Code, PATINDEX('%[0-9][0-9][0-9]%', c.Code) + 2, 1) IN ('0', '2', '4', '6', '8') THEN 2
                    ELSE 1
                END
            ELSE 1
        END AS TargetSemester
    FROM Courses c;

    PRINT '>>> Step 2: Finding sessions where BOTH a target-semester and wrong-semester offering exist...';

    IF OBJECT_ID('tempdb..#DuplicateOfferings') IS NOT NULL DROP TABLE #DuplicateOfferings;
    SELECT 
        wrong.Id AS WrongOfferingId,
        target.Id AS TargetOfferingId
    INTO #DuplicateOfferings
    FROM CourseOfferings wrong
    JOIN #CourseRules cr ON wrong.CourseId = cr.CourseId
    JOIN CourseOfferings target 
        ON target.CourseId = wrong.CourseId 
       AND target.AcademicSessionId = wrong.AcademicSessionId
       AND target.Semester = cr.TargetSemester
    WHERE wrong.Semester <> cr.TargetSemester;

    DECLARE @dupCount INT = (SELECT COUNT(*) FROM #DuplicateOfferings);
    PRINT 'Found ' + CAST(@dupCount AS VARCHAR) + ' courses with conflicting offerings in both semesters.';

    IF @dupCount > 0
    BEGIN
        PRINT '>>> Step 3: Merging dependent data into target-semester offerings...';

        -- Re-link CourseEnrollments: delete duplicate if student already enrolled in target
        DELETE ce
        FROM CourseEnrollments ce
        JOIN #DuplicateOfferings d ON ce.CourseOfferingId = d.WrongOfferingId
        WHERE EXISTS (
            SELECT 1 FROM CourseEnrollments targetCe
            WHERE targetCe.CourseOfferingId = d.TargetOfferingId
              AND targetCe.StudentId = ce.StudentId
        );

        UPDATE ce
        SET ce.CourseOfferingId = d.TargetOfferingId
        FROM CourseEnrollments ce
        JOIN #DuplicateOfferings d ON ce.CourseOfferingId = d.WrongOfferingId;
        PRINT 'Re-linked CourseEnrollments.';

        -- Re-link CourseOfferingPrograms: delete duplicate if (program, level) already on target
        DELETE cop
        FROM CourseOfferingPrograms cop
        JOIN #DuplicateOfferings d ON cop.CourseOfferingId = d.WrongOfferingId
        WHERE EXISTS (
            SELECT 1 FROM CourseOfferingPrograms targetCop
            WHERE targetCop.CourseOfferingId = d.TargetOfferingId
              AND targetCop.ProgramId = cop.ProgramId
              AND targetCop.LevelId = cop.LevelId
        );

        UPDATE cop
        SET cop.CourseOfferingId = d.TargetOfferingId
        FROM CourseOfferingPrograms cop
        JOIN #DuplicateOfferings d ON cop.CourseOfferingId = d.WrongOfferingId;
        PRINT 'Re-linked CourseOfferingPrograms.';

        -- Re-link CourseOfferingLecturers: delete duplicate if lecturer already on target
        DELETE col
        FROM CourseOfferingLecturers col
        JOIN #DuplicateOfferings d ON col.CourseOfferingId = d.WrongOfferingId
        WHERE EXISTS (
            SELECT 1 FROM CourseOfferingLecturers targetCol
            WHERE targetCol.CourseOfferingId = d.TargetOfferingId
              AND targetCol.LecturerId = col.LecturerId
        );

        UPDATE col
        SET col.CourseOfferingId = d.TargetOfferingId
        FROM CourseOfferingLecturers col
        JOIN #DuplicateOfferings d ON col.CourseOfferingId = d.WrongOfferingId;
        PRINT 'Re-linked CourseOfferingLecturers.';

        -- Re-link StudentCourseResults: delete duplicate
        DELETE scr
        FROM StudentCourseResults scr
        JOIN #DuplicateOfferings d ON scr.CourseOfferingId = d.WrongOfferingId
        WHERE EXISTS (
            SELECT 1 FROM StudentCourseResults targetScr
            WHERE targetScr.CourseOfferingId = d.TargetOfferingId
              AND targetScr.StudentId = scr.StudentId
        );

        UPDATE scr
        SET scr.CourseOfferingId = d.TargetOfferingId
        FROM StudentCourseResults scr
        JOIN #DuplicateOfferings d ON scr.CourseOfferingId = d.WrongOfferingId;
        PRINT 'Re-linked StudentCourseResults.';

        -- Re-link AssessmentCategories & Assessments
        IF OBJECT_ID('dbo.AssessmentCategories') IS NOT NULL
        BEGIN
            UPDATE ac
            SET ac.CourseOfferingId = d.TargetOfferingId
            FROM AssessmentCategories ac
            JOIN #DuplicateOfferings d ON ac.CourseOfferingId = d.WrongOfferingId;
            PRINT 'Re-linked AssessmentCategories.';
        END;

        IF OBJECT_ID('dbo.Assessments') IS NOT NULL
        BEGIN
            UPDATE a
            SET a.CourseOfferingId = d.TargetOfferingId
            FROM Assessments a
            JOIN #DuplicateOfferings d ON a.CourseOfferingId = d.WrongOfferingId;
            PRINT 'Re-linked Assessments.';
        END;

        -- Re-link LectureSessions
        IF OBJECT_ID('dbo.LectureSessions') IS NOT NULL
        BEGIN
            UPDATE ls SET ls.CourseOfferingId = d.TargetOfferingId
            FROM LectureSessions ls JOIN #DuplicateOfferings d ON ls.CourseOfferingId = d.WrongOfferingId;
            PRINT 'Re-linked LectureSessions.';
        END;

        -- Re-link GradeApprovals: delete duplicate on (CourseOfferingId, Level) first
        IF OBJECT_ID('dbo.GradeApprovals') IS NOT NULL
        BEGIN
            DELETE ga
            FROM GradeApprovals ga
            JOIN #DuplicateOfferings d ON ga.CourseOfferingId = d.WrongOfferingId
            WHERE EXISTS (
                SELECT 1 FROM GradeApprovals targetGa
                WHERE targetGa.CourseOfferingId = d.TargetOfferingId
                  AND targetGa.Level = ga.Level
            );

            UPDATE ga SET ga.CourseOfferingId = d.TargetOfferingId
            FROM GradeApprovals ga JOIN #DuplicateOfferings d ON ga.CourseOfferingId = d.WrongOfferingId;
            PRINT 'Re-linked GradeApprovals.';
        END;

        -- Re-link GradePublications: delete duplicate on CourseOfferingId first
        IF OBJECT_ID('dbo.GradePublications') IS NOT NULL
        BEGIN
            DELETE gp
            FROM GradePublications gp
            JOIN #DuplicateOfferings d ON gp.CourseOfferingId = d.WrongOfferingId
            WHERE EXISTS (
                SELECT 1 FROM GradePublications targetGp
                WHERE targetGp.CourseOfferingId = d.TargetOfferingId
            );

            UPDATE gp SET gp.CourseOfferingId = d.TargetOfferingId
            FROM GradePublications gp JOIN #DuplicateOfferings d ON gp.CourseOfferingId = d.WrongOfferingId;
            PRINT 'Re-linked GradePublications.';
        END;

        -- Re-link Quizzes
        IF OBJECT_ID('dbo.Quizzes') IS NOT NULL
        BEGIN
            UPDATE q SET q.CourseOfferingId = d.TargetOfferingId
            FROM Quizzes q JOIN #DuplicateOfferings d ON q.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link Assignments
        IF OBJECT_ID('dbo.Assignments') IS NOT NULL
        BEGIN
            UPDATE asm SET asm.CourseOfferingId = d.TargetOfferingId
            FROM Assignments asm JOIN #DuplicateOfferings d ON asm.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link LectureTimetableSlots
        IF OBJECT_ID('dbo.LectureTimetableSlots') IS NOT NULL
        BEGIN
            UPDATE lts SET lts.CourseOfferingId = d.TargetOfferingId
            FROM LectureTimetableSlots lts JOIN #DuplicateOfferings d ON lts.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link CourseMaterials
        IF OBJECT_ID('dbo.CourseMaterials') IS NOT NULL
        BEGIN
            UPDATE cm SET cm.CourseOfferingId = d.TargetOfferingId
            FROM CourseMaterials cm JOIN #DuplicateOfferings d ON cm.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link DiscussionThreads
        IF OBJECT_ID('dbo.DiscussionThreads') IS NOT NULL
        BEGIN
            UPDATE dt SET dt.CourseOfferingId = d.TargetOfferingId
            FROM DiscussionThreads dt JOIN #DuplicateOfferings d ON dt.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link PrerequisiteOverrides & Waitlists
        IF OBJECT_ID('dbo.PrerequisiteOverrides') IS NOT NULL
        BEGIN
            UPDATE po SET po.CourseOfferingId = d.TargetOfferingId
            FROM PrerequisiteOverrides po JOIN #DuplicateOfferings d ON po.CourseOfferingId = d.WrongOfferingId;
        END;

        IF OBJECT_ID('dbo.Waitlists') IS NOT NULL
        BEGIN
            UPDATE w SET w.CourseOfferingId = d.TargetOfferingId
            FROM Waitlists w JOIN #DuplicateOfferings d ON w.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Re-link CourseSwapRequests
        IF OBJECT_ID('dbo.CourseSwapRequests') IS NOT NULL
        BEGIN
            UPDATE csr SET csr.CourseOfferingToAddId = d.TargetOfferingId
            FROM CourseSwapRequests csr JOIN #DuplicateOfferings d ON csr.CourseOfferingToAddId = d.WrongOfferingId;

            UPDATE csr SET csr.CourseOfferingToDropId = d.TargetOfferingId
            FROM CourseSwapRequests csr JOIN #DuplicateOfferings d ON csr.CourseOfferingToDropId = d.WrongOfferingId;
        END;

        -- Re-link ClassterResultUploadRows & QuestionBanks
        IF OBJECT_ID('dbo.ClassterResultUploadRows') IS NOT NULL
        BEGIN
            UPDATE crur SET crur.CourseOfferingId = d.TargetOfferingId
            FROM ClassterResultUploadRows crur JOIN #DuplicateOfferings d ON crur.CourseOfferingId = d.WrongOfferingId;
        END;

        IF OBJECT_ID('dbo.QuestionBanks') IS NOT NULL
        BEGIN
            UPDATE qb SET qb.CourseOfferingId = d.TargetOfferingId
            FROM QuestionBanks qb JOIN #DuplicateOfferings d ON qb.CourseOfferingId = d.WrongOfferingId;
        END;

        -- Delete the wrong offering row
        DELETE co
        FROM CourseOfferings co
        JOIN #DuplicateOfferings d ON co.Id = d.WrongOfferingId;
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' redundant wrong-semester CourseOfferings.';
    END

    DROP TABLE #DuplicateOfferings;

    PRINT '>>> Step 4: Updating remaining wrong-semester CourseOfferings to target semester...';

    UPDATE co
    SET co.Semester = cr.TargetSemester
    FROM CourseOfferings co
    JOIN #CourseRules cr ON co.CourseId = cr.CourseId
    WHERE co.Semester <> cr.TargetSemester;
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' single wrong-semester CourseOfferings to correct TargetSemester.';

    DROP TABLE #CourseRules;

    COMMIT TRANSACTION;
    PRINT '>>> SUCCESS: CourseOffering semesters successfully aligned!';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '>>> ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
