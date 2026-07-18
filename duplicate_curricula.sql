BEGIN TRANSACTION;
BEGIN TRY

    DECLARE @Session2425 UNIQUEIDENTIFIER;
    DECLARE @Session2526 UNIQUEIDENTIFIER;

    -- 1. Dynamically resolve the session IDs based on their names
    SELECT TOP 1 @Session2425 = Id FROM AcademicSessions WHERE Name LIKE '%2024/2025%';
    SELECT TOP 1 @Session2526 = Id FROM AcademicSessions WHERE Name LIKE '%2025/2026%';

    IF @Session2425 IS NULL OR @Session2526 IS NULL
    BEGIN
        THROW 50000, 'Could not find Academic Sessions for 2024/2025 or 2025/2026', 1;
    END

    -- Tracking variables
    DECLARE @CurriculaDuplicated INT = 0;
    DECLARE @CoursesDuplicated INT = 0;

    -- Temporary table to hold curricula that need duplicating from Source to Target
    CREATE TABLE #CurriculaToDuplicate (
        SourceCurriculumId UNIQUEIDENTIFIER,
        TargetCurriculumId UNIQUEIDENTIFIER,
        TargetSessionId UNIQUEIDENTIFIER,
        ProgramId UNIQUEIDENTIFIER,
        Name NVARCHAR(MAX),
        MinCreditUnitsForGraduation INT,
        Status INT,
        ParentCurriculumId UNIQUEIDENTIFIER,
        IsActive BIT,
        CreatedUtc DATETIME2
    );

    --------------------------------------------------------------------------------
    -- A. Find Curricula in 25/26 that DO NOT exist in 24/25, and prepare to copy to 24/25
    --------------------------------------------------------------------------------
    INSERT INTO #CurriculaToDuplicate 
    SELECT 
        c.Id AS SourceCurriculumId,
        NEWID() AS TargetCurriculumId,
        @Session2425 AS TargetSessionId,
        c.ProgramId,
        c.Name,
        c.MinCreditUnitsForGraduation,
        c.Status,
        c.ParentCurriculumId,
        c.IsActive,
        GETUTCDATE()
    FROM Curricula c
    WHERE c.AdmissionSessionId = @Session2526
      AND NOT EXISTS (
          SELECT 1 FROM Curricula c2 
          WHERE c2.ProgramId = c.ProgramId 
            AND c2.AdmissionSessionId = @Session2425
      );

    --------------------------------------------------------------------------------
    -- B. Find Curricula in 24/25 that DO NOT exist in 25/26, and prepare to copy to 25/26
    --------------------------------------------------------------------------------
    INSERT INTO #CurriculaToDuplicate 
    SELECT 
        c.Id AS SourceCurriculumId,
        NEWID() AS TargetCurriculumId,
        @Session2526 AS TargetSessionId,
        c.ProgramId,
        c.Name,
        c.MinCreditUnitsForGraduation,
        c.Status,
        c.ParentCurriculumId,
        c.IsActive,
        GETUTCDATE()
    FROM Curricula c
    WHERE c.AdmissionSessionId = @Session2425
      AND NOT EXISTS (
          SELECT 1 FROM Curricula c2 
          WHERE c2.ProgramId = c.ProgramId 
            AND c2.AdmissionSessionId = @Session2526
      );

    --------------------------------------------------------------------------------
    -- 2. Insert the duplicated Curricula
    --------------------------------------------------------------------------------
    INSERT INTO Curricula (Id, ProgramId, AdmissionSessionId, Name, MinCreditUnitsForGraduation, Status, ParentCurriculumId, IsActive, CreatedUtc)
    SELECT 
        TargetCurriculumId, ProgramId, TargetSessionId, Name, MinCreditUnitsForGraduation, Status, ParentCurriculumId, IsActive, CreatedUtc
    FROM #CurriculaToDuplicate;
    
    SET @CurriculaDuplicated = @@ROWCOUNT;

    --------------------------------------------------------------------------------
    -- 3. Duplicate all CurriculumCourses mapped to the old curricula into the new curricula
    --------------------------------------------------------------------------------
    INSERT INTO CurriculumCourses (Id, CurriculumId, LevelId, CourseId, Semester, Category, CreditUnits)
    SELECT 
        NEWID() AS Id,
        t.TargetCurriculumId AS CurriculumId,
        cc.LevelId,
        cc.CourseId,
        cc.Semester,
        cc.Category,
        cc.CreditUnits
    FROM CurriculumCourses cc
    JOIN #CurriculaToDuplicate t ON cc.CurriculumId = t.SourceCurriculumId;

    SET @CoursesDuplicated = @@ROWCOUNT;

    -- Clean up
    DROP TABLE #CurriculaToDuplicate;

    COMMIT TRANSACTION;
    
    PRINT 'Successfully duplicated ' + CAST(@CurriculaDuplicated AS VARCHAR) + ' Curricula.';
    PRINT 'Successfully duplicated ' + CAST(@CoursesDuplicated AS VARCHAR) + ' CurriculumCourses.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT 'Error occurred: ' + ERROR_MESSAGE();
    THROW;
END CATCH
