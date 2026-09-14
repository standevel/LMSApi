-- ==============================================================================
-- SQL Remediation Script: Correct Course Level and Semester Mismatches
-- System: Wigwe University LMS
-- Purpose:
--   1. Re-align Year 1 (100 Level) courses currently incorrectly assigned to 200 Level (Year 2)
--      in CourseOfferingPrograms, Courses, and CurriculumCourses.
--   2. Explicitly cover all 98 Classter migration courses (all confirmed 100 Level).
--   3. Apply academic level rule based on first digit of 3-digit course number:
--        - 1xx -> 100 Level (Order = 1)
--        - 2xx -> 200 Level (Order = 2)
--        - 3xx -> 300 Level (Order = 3)
--        - 4xx -> 400 Level (Order = 4)
--   4. Apply semester rule based on parity of the last digit of the course number:
--        - Odd digit (1, 3, 5, 7, 9) -> First Semester (Semester = 1)
--        - Even digit (0, 2, 4, 6, 8) -> Second Semester (Semester = 2)
--   5. Safely deduplicate CourseOfferingProgram and CurriculumCourse rows before re-aligning
--      to prevent unique key / foreign key conflicts.
-- ==============================================================================

BEGIN TRANSACTION;
BEGIN TRY

    PRINT '>>> Step 1: Building list of confirmed Classter 100L courses and course code patterns...';

    -- Create temporary table for explicit Classter courses
    IF OBJECT_ID('tempdb..#ClassterCourses') IS NOT NULL DROP TABLE #ClassterCourses;
    CREATE TABLE #ClassterCourses (
        CodePattern NVARCHAR(50) PRIMARY KEY
    );

    INSERT INTO #ClassterCourses (CodePattern) VALUES
    ('ACC 101'), ('ACC 102'), ('AMS 101'), ('AMS 102'), ('AMS 103'), ('AMS 104'),
    ('BUA 101'), ('BUA 102'), ('CHM 101'), ('CHM 102'), ('CHM 107'), ('CHM 108'),
    ('CMS 101'), ('CMS 102'), ('COS 101'), ('COS 102'), ('CPE 102'), ('DAT 101'),
    ('DAT 102'), ('ECO 101'), ('ECO 102'), ('ECO 103'), ('ECO 104'), ('ENT 102'),
    ('ENT 121'), ('ENT 122'), ('ENT 124'), ('ENT 125'), ('FAA 111'), ('FAA 112'),
    ('FAA 121'), ('FAA 131'), ('FIN 101'), ('FIP 101'), ('FIP 102'), ('FMM 101'),
    ('FMM 102'), ('FRS 102'), ('GET 101'), ('GET 102'), ('GST 111'), ('GST 112'),
    ('LIT 107'), ('MCE 101'), ('MEE 101'), ('MTH 101'), ('MTH 102'), ('MTH 103'),
    ('MTH 104'), ('PHY 101'), ('PHY 102'), ('PHY 104'), ('PHY 107'), ('PHY 108'),
    ('STA 111'), ('STA 112'), ('TEL 100'), ('THA 101'), ('THA 102'), ('THA 103'),
    ('THA 104'), ('THA 105'), ('THA 106'), ('WU 100'), ('WU 111'), ('WU CA 105'),
    ('WU CA 108'), ('WU CA 124'), ('WU CA 152'), ('WU CDM 103'), ('WU CDM 105'),
    ('WU CDM 107'), ('WU CDM 122'), ('WU CDM 152'), ('WU CYB 113'), ('WU DTS 100'),
    ('WU ECO 106'), ('WU ECO 108'), ('WU ECO 110'), ('WU ECO-110'), ('WU FAD 104'),
    ('WU FAD 122'), ('WU FAD 124'), ('WU FMS 104'), ('WU FMS 101'), ('WU FMS-101'),
    ('WU GET 108'), ('WU GET 112'), ('WU GST 105'), ('WU GST 116'), ('WU MTH 106'),
    ('WU RAI 102'), ('WU RAI 103'), ('WU RAI 122'), ('WU RAI 101'), ('WU RAI-101'),
    ('WU THA 107'), ('WU THA 122'), ('WU THA 142'), ('WU-ECO 108'), ('WU-GST 111');

    PRINT '>>> Step 2: Mapping all courses to their intended Academic Level Order and Semester...';

    IF OBJECT_ID('tempdb..#CourseRules') IS NOT NULL DROP TABLE #CourseRules;
    CREATE TABLE #CourseRules (
        CourseId UNIQUEIDENTIFIER PRIMARY KEY,
        Code NVARCHAR(100),
        TargetLevelOrder INT,
        TargetSemester INT
    );

    INSERT INTO #CourseRules (CourseId, Code, TargetLevelOrder, TargetSemester)
    SELECT 
        c.Id AS CourseId,
        c.Code,
        -- Level Order:
        CASE 
            -- Check explicit Classter whitelist first (always Year 1 / Order 1)
            WHEN EXISTS (
                SELECT 1 FROM #ClassterCourses cc 
                WHERE REPLACE(c.Code, '-', ' ') = REPLACE(cc.CodePattern, '-', ' ')
                   OR c.Code = cc.CodePattern
            ) THEN 1
            -- Otherwise inspect first digit of the 3-digit course number
            WHEN PATINDEX('%[0-9][0-9][0-9]%', c.Code) > 0 THEN
                CAST(SUBSTRING(c.Code, PATINDEX('%[0-9][0-9][0-9]%', c.Code), 1) AS INT)
            ELSE 1
        END AS TargetLevelOrder,
        -- Semester: Odd = 1, Even = 2
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

    PRINT '>>> Step 3: Resolving Target Level IDs per program...';

    -- Create deduplicated ProgramLevelMap (guaranteeing at most 1 level per ProgramId and LevelOrder)
    IF OBJECT_ID('tempdb..#ProgramLevelMap') IS NOT NULL DROP TABLE #ProgramLevelMap;
    SELECT 
        l.Id AS LevelId,
        l.ProgramId,
        l.[Order] AS LevelOrder,
        l.Name AS LevelName
    INTO #ProgramLevelMap
    FROM (
        SELECT 
            Id,
            ProgramId,
            [Order],
            Name,
            ROW_NUMBER() OVER (PARTITION BY ProgramId, [Order] ORDER BY Id) as rn
        FROM Levels
    ) l
    WHERE l.rn = 1;

    CREATE UNIQUE CLUSTERED INDEX IX_TempProgramLevelMap ON #ProgramLevelMap (ProgramId, LevelOrder);

    -- =========================================================================
    -- STEP 4: FIX CourseOfferings Semester
    -- =========================================================================
    PRINT '>>> Step 4: Re-aligning CourseOfferings to match course code semester parity...';

    -- 4a. Find sessions where BOTH a target-semester offering and a wrong-semester offering exist
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

    -- Re-link CourseEnrollments: delete dupes where student already enrolled in target
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

    -- Re-link CourseOfferingPrograms: delete dupes where (program, level) already on target
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

    -- Re-link CourseOfferingLecturers: delete dupes
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

    -- Re-link StudentCourseResults: delete dupes
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
    PRINT 'Merged and deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' redundant wrong-semester CourseOfferings.';

    DROP TABLE #DuplicateOfferings;

    -- 4b. Where ONLY a wrong-semester offering exists, safely update its Semester to TargetSemester
    UPDATE co
    SET co.Semester = cr.TargetSemester
    FROM CourseOfferings co
    JOIN #CourseRules cr ON co.CourseId = cr.CourseId
    WHERE co.Semester <> cr.TargetSemester;
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' single wrong-semester CourseOfferings to correct TargetSemester.';

    -- =========================================================================
    -- STEP 5: FIX CourseOfferingPrograms LevelId
    -- =========================================================================
    PRINT '>>> Step 5: Correcting CourseOfferingPrograms...';

    -- 4a. Delete redundant mismatched COP rows if a row with TargetLevelId already exists for this offering & program
    DELETE cop
    FROM CourseOfferingPrograms cop
    JOIN CourseOfferings co ON cop.CourseOfferingId = co.Id
    JOIN #CourseRules cr ON co.CourseId = cr.CourseId
    JOIN #ProgramLevelMap targetLevel ON cop.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
    WHERE cop.LevelId <> targetLevel.LevelId
      AND EXISTS (
          SELECT 1 FROM CourseOfferingPrograms existing
          WHERE existing.CourseOfferingId = cop.CourseOfferingId
            AND existing.ProgramId = cop.ProgramId
            AND existing.LevelId = targetLevel.LevelId
      );
    PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' redundant COP entries where target level already exists.';

    -- 4b. If multiple mismatched rows exist for the same (CourseOfferingId, ProgramId), keep only 1 to prevent unique index violation
    ;WITH RankedCOPDuplicates AS (
        SELECT 
            cop.Id,
            ROW_NUMBER() OVER (
                PARTITION BY cop.CourseOfferingId, cop.ProgramId 
                ORDER BY cop.Id
            ) AS RowNum
        FROM CourseOfferingPrograms cop
        JOIN CourseOfferings co ON cop.CourseOfferingId = co.Id
        JOIN #CourseRules cr ON co.CourseId = cr.CourseId
        JOIN #ProgramLevelMap targetLevel ON cop.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
        WHERE cop.LevelId <> targetLevel.LevelId
    )
    DELETE cop
    FROM CourseOfferingPrograms cop
    JOIN RankedCOPDuplicates rd ON cop.Id = rd.Id
    WHERE rd.RowNum > 1;
    PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' duplicate mismatched COP entries.';

    -- 4c. Update remaining mismatched COP rows to TargetLevelId
    UPDATE cop
    SET cop.LevelId = targetLevel.LevelId
    FROM CourseOfferingPrograms cop
    JOIN CourseOfferings co ON cop.CourseOfferingId = co.Id
    JOIN #CourseRules cr ON co.CourseId = cr.CourseId
    JOIN #ProgramLevelMap targetLevel ON cop.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
    WHERE cop.LevelId <> targetLevel.LevelId;
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' CourseOfferingProgram records to correct level.';

    -- =========================================================================
    -- STEP 5: FIX Courses table LevelId and Semester
    -- =========================================================================
    PRINT '>>> Step 5: Aligning Courses table LevelId and Semester...';

    UPDATE c
    SET c.LevelId = targetLevel.LevelId,
        c.Semester = cr.TargetSemester
    FROM Courses c
    JOIN #CourseRules cr ON c.Id = cr.CourseId
    LEFT JOIN #ProgramLevelMap targetLevel ON c.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
    WHERE (c.LevelId IS NULL OR c.LevelId <> targetLevel.LevelId)
       OR (c.Semester IS NULL OR c.Semester <> cr.TargetSemester);

    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' Courses table records.';

    -- =========================================================================
    -- STEP 6: FIX CurriculumCourses table LevelId and Semester
    -- =========================================================================
    PRINT '>>> Step 6: Aligning CurriculumCourses table LevelId and Semester...';

    -- 6a. Delete redundant mismatched rows if target (CurriculumId, CourseId, TargetLevelId, TargetSemester) already exists
    DELETE cc
    FROM CurriculumCourses cc
    JOIN Curricula cur ON cc.CurriculumId = cur.Id
    JOIN #CourseRules cr ON cc.CourseId = cr.CourseId
    JOIN #ProgramLevelMap targetLevel ON cur.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
    WHERE (cc.LevelId <> targetLevel.LevelId OR cc.Semester <> cr.TargetSemester)
      AND EXISTS (
          SELECT 1 FROM CurriculumCourses existing
          WHERE existing.CurriculumId = cc.CurriculumId
            AND existing.CourseId = cc.CourseId
            AND existing.LevelId = targetLevel.LevelId
            AND existing.Semester = cr.TargetSemester
      );
    PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' redundant CurriculumCourses entries where target already exists.';

    -- 6b. If multiple mismatched rows exist for the same (CurriculumId, CourseId), keep only 1
    ;WITH RankedCCDuplicates AS (
        SELECT 
            cc.Id,
            ROW_NUMBER() OVER (
                PARTITION BY cc.CurriculumId, cc.CourseId 
                ORDER BY cc.Id
            ) AS RowNum
        FROM CurriculumCourses cc
        JOIN Curricula cur ON cc.CurriculumId = cur.Id
        JOIN #CourseRules cr ON cc.CourseId = cr.CourseId
        JOIN #ProgramLevelMap targetLevel ON cur.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
        WHERE (cc.LevelId <> targetLevel.LevelId OR cc.Semester <> cr.TargetSemester)
    )
    DELETE cc
    FROM CurriculumCourses cc
    JOIN RankedCCDuplicates rd ON cc.Id = rd.Id
    WHERE rd.RowNum > 1;
    PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' duplicate mismatched CurriculumCourses entries.';

    -- 6c. Update remaining CurriculumCourses
    UPDATE cc
    SET cc.LevelId = targetLevel.LevelId,
        cc.Semester = cr.TargetSemester
    FROM CurriculumCourses cc
    JOIN Curricula cur ON cc.CurriculumId = cur.Id
    JOIN #CourseRules cr ON cc.CourseId = cr.CourseId
    JOIN #ProgramLevelMap targetLevel ON cur.ProgramId = targetLevel.ProgramId AND targetLevel.LevelOrder = cr.TargetLevelOrder
    WHERE (cc.LevelId <> targetLevel.LevelId OR cc.Semester <> cr.TargetSemester);
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' CurriculumCourses records to correct Level & Semester.';

    -- =========================================================================
    -- STEP 7: CLEANUP
    -- =========================================================================
    DROP TABLE #ClassterCourses;
    DROP TABLE #CourseRules;
    DROP TABLE #ProgramLevelMap;

    COMMIT TRANSACTION;
    PRINT '>>> SUCCESS: Course level and semester alignment script completed successfully!';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '>>> ERROR: An error occurred while executing the course repair script: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
