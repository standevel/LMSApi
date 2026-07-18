BEGIN TRANSACTION;
BEGIN TRY

    DECLARE @Session2425 UNIQUEIDENTIFIER;
    DECLARE @Session2526 UNIQUEIDENTIFIER;

    -- Dynamically resolve the session IDs based on their names
    SELECT TOP 1 @Session2425 = Id FROM AcademicSessions WHERE Name LIKE '%2024/2025%';
    SELECT TOP 1 @Session2526 = Id FROM AcademicSessions WHERE Name LIKE '%2025/2026%';

    IF @Session2425 IS NULL OR @Session2526 IS NULL
    BEGIN
        THROW 50000, 'Could not find Academic Sessions for 2024/2025 or 2025/2026', 1;
    END

    -- Fix 1: "all 200 levels in 2024/2025 are supposed to be 200 level in 2025/2026 and 100 level in the 2024/2025 session"
    SELECT e.Id as EnrollmentId, e.UserId, e.ProgramId, e.LevelId, e.CurriculumId,
           l100.Id as Level100Id, l200.Id as Level200Id
    INTO #Affected200L
    FROM Enrollments e
    JOIN Levels l200 ON e.LevelId = l200.Id
    JOIN Levels l100 ON l200.ProgramId = l100.ProgramId AND l100.Name = '100 Level'
    WHERE e.AcademicSessionId = @Session2425
      AND l200.Name = '200 Level';

    DECLARE @inserted INT = 0;
    DECLARE @updated INT = 0;

    -- Create 200L in 25/26 for these students if they don't already have an enrollment
    INSERT INTO Enrollments (Id, UserId, AcademicSessionId, ProgramId, LevelId, CurriculumId)
    SELECT NEWID(), UserId, @Session2526, ProgramId, Level200Id, CurriculumId
    FROM #Affected200L a
    WHERE NOT EXISTS (
        SELECT 1 FROM Enrollments e2 
        WHERE e2.UserId = a.UserId 
          AND e2.AcademicSessionId = @Session2526
    );
    SET @inserted = @@ROWCOUNT;

    -- Update their 24/25 enrollment back to 100L
    UPDATE e
    SET e.LevelId = a.Level100Id
    FROM Enrollments e
    JOIN #Affected200L a ON e.Id = a.EnrollmentId;
    SET @updated = @@ROWCOUNT;

    DROP TABLE #Affected200L;

    -- Fix 2: "while the 100 level students in 2025/2026 are not supposed to be in 2024/2025 since they were admitted in 2025/2026 session"
    SELECT e.UserId
    INTO #Affected100L
    FROM Enrollments e
    JOIN Levels l ON e.LevelId = l.Id
    WHERE e.AcademicSessionId = @Session2526
      AND l.Name = '100 Level';

    DECLARE @deletedCourses INT = 0;
    DECLARE @deletedEnrollments INT = 0;

    -- Delete any registered courses they might have mistakenly had in 24/25
    DELETE ce
    FROM CourseEnrollments ce
    JOIN CourseOfferings co ON ce.CourseOfferingId = co.Id
    JOIN #Affected100L a ON ce.StudentId = a.UserId
    WHERE co.AcademicSessionId = @Session2425;
    SET @deletedCourses = @@ROWCOUNT;

    -- Delete their program enrollment in 24/25
    DELETE e
    FROM Enrollments e
    JOIN #Affected100L a ON e.UserId = a.UserId
    WHERE e.AcademicSessionId = @Session2425;
    SET @deletedEnrollments = @@ROWCOUNT;

    DROP TABLE #Affected100L;

    COMMIT TRANSACTION;
    
    PRINT 'Inserted 25/26 200L enrollments: ' + CAST(@inserted AS VARCHAR);
    PRINT 'Updated 24/25 200L to 100L: ' + CAST(@updated AS VARCHAR);
    PRINT 'Deleted 24/25 Course Enrollments for 25/26 100L students: ' + CAST(@deletedCourses AS VARCHAR);
    PRINT 'Deleted 24/25 Program Enrollments for 25/26 100L students: ' + CAST(@deletedEnrollments AS VARCHAR);

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT 'Error occurred: ' + ERROR_MESSAGE();
    THROW;
END CATCH
