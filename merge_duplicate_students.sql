BEGIN TRANSACTION;
BEGIN TRY

    -- Temporary table to map Unofficial to Official Student IDs
    CREATE TABLE #StudentMerges (
        OfficialStudentId UNIQUEIDENTIFIER,
        UnofficialStudentId UNIQUEIDENTIFIER,
        FirstName NVARCHAR(255),
        LastName NVARCHAR(255)
    );

    -- Find duplicates by FirstName and LastName where one has @wigweuniversity.edu.ng and the other doesn't
    INSERT INTO #StudentMerges (OfficialStudentId, UnofficialStudentId, FirstName, LastName)
    SELECT 
        sOfficial.Id AS OfficialStudentId,
        sUnofficial.Id AS UnofficialStudentId,
        sOfficial.FirstName,
        sOfficial.LastName
    FROM Students sOfficial
    JOIN Students sUnofficial 
        ON sOfficial.FirstName = sUnofficial.FirstName 
        AND sOfficial.LastName = sUnofficial.LastName
        AND sOfficial.Id != sUnofficial.Id
    WHERE 
        sOfficial.OfficialEmail LIKE '%@wigweuniversity.edu.ng%'
        AND sUnofficial.OfficialEmail NOT LIKE '%@wigweuniversity.edu.ng%';

    DECLARE @MergeCount INT = (SELECT COUNT(*) FROM #StudentMerges);
    PRINT 'Found ' + CAST(@MergeCount AS VARCHAR) + ' duplicate student pairs to merge.';

    ----------------------------------------------------------
    -- 1. Merge Enrollments (ProgramEnrollments) -> links to Users
    ----------------------------------------------------------
    UPDATE e
    SET e.UserId = m.OfficialStudentId
    FROM Enrollments e
    JOIN #StudentMerges m ON e.UserId = m.UnofficialStudentId
    WHERE NOT EXISTS (
        SELECT 1 FROM Enrollments e2
        WHERE e2.UserId = m.OfficialStudentId
          AND e2.AcademicSessionId = e.AcademicSessionId
          AND e2.ProgramId = e.ProgramId
    );
    -- Delete redundant if already exists
    DELETE e
    FROM Enrollments e
    JOIN #StudentMerges m ON e.UserId = m.UnofficialStudentId;

    ----------------------------------------------------------
    -- 2. Merge CourseEnrollments -> links to Users
    ----------------------------------------------------------
    UPDATE ce
    SET ce.StudentId = m.OfficialStudentId
    FROM CourseEnrollments ce
    JOIN #StudentMerges m ON ce.StudentId = m.UnofficialStudentId
    WHERE NOT EXISTS (
        SELECT 1 FROM CourseEnrollments ce2
        WHERE ce2.StudentId = m.OfficialStudentId
          AND ce2.CourseOfferingId = ce.CourseOfferingId
    );
    DELETE ce
    FROM CourseEnrollments ce
    JOIN #StudentMerges m ON ce.StudentId = m.UnofficialStudentId;

    ----------------------------------------------------------
    -- 3. Merge StudentFeeRecords -> links to Students
    ----------------------------------------------------------
    UPDATE sfr
    SET sfr.StudentId = m.OfficialStudentId
    FROM StudentFeeRecords sfr
    JOIN #StudentMerges m ON sfr.StudentId = m.UnofficialStudentId;

    ----------------------------------------------------------
    -- 4. Merge FeeAssignments -> links to Users
    ----------------------------------------------------------
    UPDATE fa
    SET fa.StudentId = m.OfficialStudentId
    FROM FeeAssignments fa
    JOIN #StudentMerges m ON fa.StudentId = m.UnofficialStudentId;

    ----------------------------------------------------------
    -- 5. Merge Grades -> links to Users
    ----------------------------------------------------------
    UPDATE g
    SET g.StudentId = m.OfficialStudentId
    FROM Grades g
    JOIN #StudentMerges m ON g.StudentId = m.UnofficialStudentId
    WHERE NOT EXISTS (
        SELECT 1 FROM Grades g2
        WHERE g2.StudentId = m.OfficialStudentId
          AND g2.AssessmentId = g.AssessmentId
    );
    DELETE g
    FROM Grades g
    JOIN #StudentMerges m ON g.StudentId = m.UnofficialStudentId;

    ----------------------------------------------------------
    -- Delete the redundant (Unofficial) Student and User records
    ----------------------------------------------------------
    
    -- Some tables might have cascaded deletes or we need to clear them if they just hold metadata
    DELETE s
    FROM StudentScholarships s JOIN #StudentMerges m ON s.StudentId = m.UnofficialStudentId;

    DELETE a
    FROM AdvisingNotes a JOIN #StudentMerges m ON a.StudentId = m.UnofficialStudentId;

    DELETE p
    FROM ParentStudentLinks p JOIN #StudentMerges m ON p.StudentId = m.UnofficialStudentId;

    DELETE v
    FROM RegistrationVerifications v JOIN #StudentMerges m ON v.StudentId = m.UnofficialStudentId;

    -- Delete Students
    DELETE s
    FROM Students s
    JOIN #StudentMerges m ON s.Id = m.UnofficialStudentId;

    -- Delete UserRoles (cleanup before deleting User)
    DELETE ur
    FROM UserRoles ur
    JOIN #StudentMerges m ON ur.UserId = m.UnofficialStudentId;

    -- Delete Users (AspNetUsers)
    DELETE u
    FROM Users u
    JOIN #StudentMerges m ON u.Id = m.UnofficialStudentId;

    DROP TABLE #StudentMerges;

    COMMIT TRANSACTION;
    PRINT 'Merge completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    PRINT 'Error occurred: ' + ERROR_MESSAGE();
    THROW;
END CATCH
