BEGIN TRAN;

-- Create temporary mapping table
CREATE TABLE #LevelMapping (
    BadLevelId UNIQUEIDENTIFIER,
    ProgramId UNIQUEIDENTIFIER,
    TargetLevelName NVARCHAR(100),
    TargetLevelOrder INT,
    TargetLevelId UNIQUEIDENTIFIER NULL
);

INSERT INTO #LevelMapping (BadLevelId, ProgramId, TargetLevelName, TargetLevelOrder)
SELECT 
    Id, 
    ProgramId, 
    CASE 
        WHEN Name LIKE 'Year 1%' THEN '100 Level'
        WHEN Name LIKE 'Year 2%' THEN '200 Level'
        WHEN Name LIKE 'Year 3%' THEN '300 Level'
        WHEN Name LIKE 'Year 4%' THEN '400 Level'
        WHEN Name LIKE 'Year 5%' THEN '500 Level'
    END,
    CASE 
        WHEN Name LIKE 'Year 1%' THEN 1
        WHEN Name LIKE 'Year 2%' THEN 2
        WHEN Name LIKE 'Year 3%' THEN 3
        WHEN Name LIKE 'Year 4%' THEN 4
        WHEN Name LIKE 'Year 5%' THEN 5
    END
FROM Levels
WHERE Name LIKE 'Year %';

-- Match existing target levels
UPDATE m
SET TargetLevelId = l.Id
FROM #LevelMapping m
JOIN Levels l ON m.ProgramId = l.ProgramId AND m.TargetLevelName = l.Name;

-- Create missing target levels
DECLARE @Id UNIQUEIDENTIFIER;
DECLARE @ProgramId UNIQUEIDENTIFIER;
DECLARE @TargetName NVARCHAR(100);
DECLARE @TargetOrder INT;

DECLARE missing_cursor CURSOR FOR
SELECT DISTINCT ProgramId, TargetLevelName, TargetLevelOrder
FROM #LevelMapping
WHERE TargetLevelId IS NULL;

OPEN missing_cursor;
FETCH NEXT FROM missing_cursor INTO @ProgramId, @TargetName, @TargetOrder;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Id = NEWID();
    INSERT INTO Levels (Id, ProgramId, Name, [Order])
    VALUES (@Id, @ProgramId, @TargetName, @TargetOrder);

    -- Update mapping table
    UPDATE #LevelMapping
    SET TargetLevelId = @Id
    WHERE ProgramId = @ProgramId AND TargetLevelName = @TargetName AND TargetLevelId IS NULL;

    FETCH NEXT FROM missing_cursor INTO @ProgramId, @TargetName, @TargetOrder;
END
CLOSE missing_cursor;
DEALLOCATE missing_cursor;

-- Now all BadLevels have a TargetLevelId. Do the migration.

-- 1. Students
UPDATE s
SET LevelId = m.TargetLevelId
FROM Students s
JOIN #LevelMapping m ON s.LevelId = m.BadLevelId;

-- 2. AdmissionApplications
UPDATE a
SET StartingLevelId = m.TargetLevelId
FROM AdmissionApplications a
JOIN #LevelMapping m ON a.StartingLevelId = m.BadLevelId;

-- 3. CourseOfferingPrograms
UPDATE cop
SET LevelId = m.TargetLevelId
FROM CourseOfferingPrograms cop
JOIN #LevelMapping m ON cop.LevelId = m.BadLevelId;

-- 4. Enrollments
UPDATE e
SET LevelId = m.TargetLevelId
FROM Enrollments e
JOIN #LevelMapping m ON e.LevelId = m.BadLevelId;

-- 5. Courses (AcademicLevelId)
UPDATE c
SET AcademicLevelId = m.TargetLevelId
FROM Courses c
JOIN #LevelMapping m ON c.AcademicLevelId = m.BadLevelId;

-- 6. Courses (LevelId)
UPDATE c
SET LevelId = m.TargetLevelId
FROM Courses c
JOIN #LevelMapping m ON c.LevelId = m.BadLevelId;

-- 7. CurriculumCourses
UPDATE cc
SET LevelId = m.TargetLevelId
FROM CurriculumCourses cc
JOIN #LevelMapping m ON cc.LevelId = m.BadLevelId;

-- Cleanup LevelSemesterConfigs
DELETE lsc
FROM LevelSemesterConfigs lsc
JOIN #LevelMapping m ON lsc.LevelId = m.BadLevelId;

-- Delete Bad Levels
DELETE l
FROM Levels l
JOIN #LevelMapping m ON l.Id = m.BadLevelId;

DROP TABLE #LevelMapping;

COMMIT TRAN;
