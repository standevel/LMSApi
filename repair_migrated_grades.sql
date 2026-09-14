-- ==============================================================================
-- Migration / Repair Script: Fix Inflated Classter Grades & Re-align MaxMarks
-- Database: LMS Database (SQL Server)
-- Purpose:
--   0. Set SystemGradingConfigurations RoundingStrategy to 'Ceiling' (round up)
--   1. Re-align AssessmentCategories MaxMarks to category weights (10/10/10/70)
--   2. Sync Assessments MaxMarks to AssessmentCategories MaxMarks
--   3. Rescale inflated CA Grades (scaled out of 100 down to 10) & round up (CEILING)
--   4. Rescale inflated Exam Grades (scaled out of 100 down to 70) & round up (CEILING)
--   5. Correct StudentCourseResults (Ca1, Ca2, Ca3, ExamScore, TotalScore <= 100) with CEILING
--   6. Recalculate LetterGrade and GradePoints based on rounded up integer TotalScore
-- ==============================================================================

BEGIN TRANSACTION;

-- 0. Update SystemGradingConfigurations to RoundingStrategy = 'Ceiling' and RoundingDecimalPlaces = 0
UPDATE SystemGradingConfigurations
SET RoundingStrategy = 'Ceiling',
    RoundingDecimalPlaces = 0,
    UpdatedAt = GETUTCDATE();

PRINT 'Step 0: Updated SystemGradingConfigurations to Ceiling rounding';

-- 1. Update AssessmentCategories: Set MaxMarks = Weight (or default 10 for CA, 70 for Exam)
UPDATE ac
SET ac.MaxMarks = CASE 
    WHEN ac.CategoryType = 0 THEN ISNULL(NULLIF(ac.Weight, 0), 10.0) -- CA1
    WHEN ac.CategoryType = 1 THEN ISNULL(NULLIF(ac.Weight, 0), 10.0) -- CA2
    WHEN ac.CategoryType = 2 THEN ISNULL(NULLIF(ac.Weight, 0), 10.0) -- CA3
    WHEN ac.CategoryType = 3 THEN ISNULL(NULLIF(ac.Weight, 0), 70.0) -- Exam
    ELSE ac.MaxMarks
END
FROM AssessmentCategories ac
WHERE (ac.MaxMarks = 100.0 AND ac.CategoryType IN (0, 1, 2, 3))
   OR (ac.CategoryType IN (0, 1, 2) AND ac.MaxMarks > 30.0)
   OR (ac.CategoryType = 3 AND ac.MaxMarks > 70.0);

PRINT 'Step 1: Updated AssessmentCategories MaxMarks';

-- 2. Update Assessments: Sync MaxMarks with AssessmentCategories
UPDATE a
SET a.MaxMarks = ac.MaxMarks
FROM Assessments a
INNER JOIN AssessmentCategories ac ON a.AssessmentCategoryId = ac.Id
WHERE a.MaxMarks <> ac.MaxMarks;

PRINT 'Step 2: Synced Assessments MaxMarks';

-- 3. Rescale inflated CA Grades and round up values (CEILING)
UPDATE g
SET g.MarksObtained = CASE 
    WHEN CEILING(g.MarksObtained / 10.0) > a.MaxMarks THEN a.MaxMarks 
    ELSE CEILING(g.MarksObtained / 10.0) 
END,
    g.UpdatedAt = GETUTCDATE()
FROM Grades g
INNER JOIN Assessments a ON g.AssessmentId = a.Id
INNER JOIN AssessmentCategories ac ON a.AssessmentCategoryId = ac.Id
WHERE ac.CategoryType IN (0, 1, 2) AND g.MarksObtained > a.MaxMarks;

-- Also round up any fractional CA grades
UPDATE g
SET g.MarksObtained = CASE 
    WHEN CEILING(g.MarksObtained) > a.MaxMarks THEN a.MaxMarks 
    ELSE CEILING(g.MarksObtained) 
END,
    g.UpdatedAt = GETUTCDATE()
FROM Grades g
INNER JOIN Assessments a ON g.AssessmentId = a.Id
INNER JOIN AssessmentCategories ac ON a.AssessmentCategoryId = ac.Id
WHERE ac.CategoryType IN (0, 1, 2) AND g.MarksObtained <> CEILING(g.MarksObtained);

PRINT 'Step 3: Rescaled and rounded up CA Grades';

-- 4. Rescale inflated Exam Grades and round up values (CEILING)
UPDATE g
SET g.MarksObtained = CASE 
    WHEN CEILING((g.MarksObtained / 100.0) * a.MaxMarks) > a.MaxMarks THEN a.MaxMarks 
    ELSE CEILING((g.MarksObtained / 100.0) * a.MaxMarks) 
END,
    g.UpdatedAt = GETUTCDATE()
FROM Grades g
INNER JOIN Assessments a ON g.AssessmentId = a.Id
INNER JOIN AssessmentCategories ac ON a.AssessmentCategoryId = ac.Id
WHERE ac.CategoryType = 3 AND g.MarksObtained > a.MaxMarks;

-- Also round up any fractional Exam grades
UPDATE g
SET g.MarksObtained = CASE 
    WHEN CEILING(g.MarksObtained) > a.MaxMarks THEN a.MaxMarks 
    ELSE CEILING(g.MarksObtained) 
END,
    g.UpdatedAt = GETUTCDATE()
FROM Grades g
INNER JOIN Assessments a ON g.AssessmentId = a.Id
INNER JOIN AssessmentCategories ac ON a.AssessmentCategoryId = ac.Id
WHERE ac.CategoryType = 3 AND g.MarksObtained <> CEILING(g.MarksObtained);

PRINT 'Step 4: Rescaled and rounded up Exam Grades';

-- 5. Fix StudentCourseResults: Rescale individual components and round up with CEILING
UPDATE scr
SET 
    scr.Ca1Score = CASE WHEN scr.Ca1Score > 10.0 THEN CEILING(scr.Ca1Score / 10.0) ELSE CEILING(scr.Ca1Score) END,
    scr.Ca2Score = CASE WHEN scr.Ca2Score > 10.0 THEN CEILING(scr.Ca2Score / 10.0) ELSE CEILING(scr.Ca2Score) END,
    scr.Ca3Score = CASE WHEN scr.Ca3Score > 10.0 THEN CEILING(scr.Ca3Score / 10.0) ELSE CEILING(scr.Ca3Score) END,
    scr.ExamScore = CASE WHEN scr.ExamScore > 70.0 THEN CEILING((scr.ExamScore / 100.0) * 70.0) ELSE CEILING(scr.ExamScore) END
FROM StudentCourseResults scr;

-- Recalculate TotalScore rounded up with CEILING and bounded to 100
UPDATE scr
SET 
    scr.TotalScore = CASE 
        WHEN CEILING(ISNULL(scr.Ca1Score, 0) + ISNULL(scr.Ca2Score, 0) + ISNULL(scr.Ca3Score, 0) + ISNULL(scr.ExamScore, 0)) > 100.0 
        THEN 100.0 
        ELSE CEILING(ISNULL(scr.Ca1Score, 0) + ISNULL(scr.Ca2Score, 0) + ISNULL(scr.Ca3Score, 0) + ISNULL(scr.ExamScore, 0)) 
    END,
    scr.UpdatedAt = GETUTCDATE()
FROM StudentCourseResults scr;

-- 6. Recalculate LetterGrade and GradePoints based on corrected TotalScore
UPDATE scr
SET 
    scr.LetterGrade = CASE 
        WHEN scr.TotalScore >= 70.0 THEN 'A'
        WHEN scr.TotalScore >= 60.0 THEN 'B'
        WHEN scr.TotalScore >= 50.0 THEN 'C'
        WHEN scr.TotalScore >= 45.0 THEN 'D'
        WHEN scr.TotalScore >= 40.0 THEN 'E'
        ELSE 'F'
    END,
    scr.GradePoints = CASE 
        WHEN scr.TotalScore >= 70.0 THEN 5.0
        WHEN scr.TotalScore >= 60.0 THEN 4.0
        WHEN scr.TotalScore >= 50.0 THEN 3.0
        WHEN scr.TotalScore >= 45.0 THEN 2.0
        WHEN scr.TotalScore >= 40.0 THEN 1.0
        ELSE 0.0
    END
FROM StudentCourseResults scr;

PRINT 'Step 5 & 6: Repaired StudentCourseResults and updated Grade / GradePoints with rounded up values';

COMMIT TRANSACTION;
PRINT 'Grade repair completed successfully.';
