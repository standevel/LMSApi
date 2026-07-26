BEGIN TRANSACTION;
BEGIN TRY

    DECLARE @Session2425 UNIQUEIDENTIFIER;
    DECLARE @Session2526 UNIQUEIDENTIFIER;
    DECLARE @Session2627 UNIQUEIDENTIFIER;

    SELECT TOP 1 @Session2425 = Id FROM AcademicSessions WHERE Name LIKE '%2024/2025%';
    SELECT TOP 1 @Session2526 = Id FROM AcademicSessions WHERE Name LIKE '%2025/2026%';
    SELECT TOP 1 @Session2627 = Id FROM AcademicSessions WHERE Name LIKE '%2026/2027%';

    IF @Session2425 IS NULL OR @Session2526 IS NULL OR @Session2627 IS NULL
    BEGIN
        THROW 50000, 'Could not find one or more Academic Sessions', 1;
    END

    DECLARE @CurriculaDuplicated INT = 0;
    DECLARE @CoursesDuplicated INT = 0;

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

    -- 1. Copy 26/27 to 24/25
    INSERT INTO #CurriculaToDuplicate 
    SELECT 
        c.Id, NEWID(), @Session2425, c.ProgramId, c.Name, c.MinCreditUnitsForGraduation,
        c.Status, c.ParentCurriculumId, c.IsActive, GETUTCDATE()
    FROM Curricula c
    WHERE c.AdmissionSessionId = @Session2627
      AND NOT EXISTS (
          SELECT 1 FROM Curricula c2 
          WHERE c2.ProgramId = c.ProgramId 
            AND c2.AdmissionSessionId = @Session2425
      );

    -- 2. Copy 26/27 to 25/26
    INSERT INTO #CurriculaToDuplicate 
    SELECT 
        c.Id, NEWID(), @Session2526, c.ProgramId, c.Name, c.MinCreditUnitsForGraduation,
        c.Status, c.ParentCurriculumId, c.IsActive, GETUTCDATE()
    FROM Curricula c
    WHERE c.AdmissionSessionId = @Session2627
      AND NOT EXISTS (
          SELECT 1 FROM Curricula c2 
          WHERE c2.ProgramId = c.ProgramId 
            AND c2.AdmissionSessionId = @Session2526
      );

    -- Insert Curricula
    INSERT INTO Curricula (Id, ProgramId, AdmissionSessionId, Name, MinCreditUnitsForGraduation, Status, ParentCurriculumId, IsActive, CreatedUtc)
    SELECT TargetCurriculumId, ProgramId, TargetSessionId, Name, MinCreditUnitsForGraduation, Status, ParentCurriculumId, IsActive, CreatedUtc
    FROM #CurriculaToDuplicate;
    
    SET @CurriculaDuplicated = @@ROWCOUNT;

    -- Insert CurriculumCourses
    INSERT INTO CurriculumCourses (Id, CurriculumId, LevelId, CourseId, Semester, Category, CreditUnits)
    SELECT NEWID(), t.TargetCurriculumId, cc.LevelId, cc.CourseId, cc.Semester, cc.Category, cc.CreditUnits
    FROM CurriculumCourses cc
    JOIN #CurriculaToDuplicate t ON cc.CurriculumId = t.SourceCurriculumId;

    SET @CoursesDuplicated = @@ROWCOUNT;

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
