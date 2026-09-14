using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateAssessmentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- 1. Pre-calculate counts per category
                IF OBJECT_ID('tempdb..#CatStats') IS NOT NULL DROP TABLE #CatStats;
                SELECT 
                    ac.Id as CatId,
                    ac.CourseOfferingId,
                    ac.CategoryType,
                    ac.CreatedAt,
                    ISNULL(assCounts.Cnt, 0) as AssCount,
                    ISNULL(gradeCounts.Cnt, 0) as GradeCount
                INTO #CatStats
                FROM AssessmentCategories ac
                LEFT JOIN (
                    SELECT AssessmentCategoryId, COUNT(*) as Cnt 
                    FROM Assessments 
                    GROUP BY AssessmentCategoryId
                ) assCounts ON ac.Id = assCounts.AssessmentCategoryId
                LEFT JOIN (
                    SELECT a.AssessmentCategoryId, COUNT(g.Id) as Cnt 
                    FROM Grades g 
                    JOIN Assessments a ON g.AssessmentId = a.Id 
                    GROUP BY a.AssessmentCategoryId
                ) gradeCounts ON ac.Id = gradeCounts.AssessmentCategoryId;

                -- Rank duplicate categories: primary is the one with most grades, then most assessments, then earliest CreatedAt
                IF OBJECT_ID('tempdb..#CategoryPairs') IS NOT NULL DROP TABLE #CategoryPairs;
                WITH CatRanked AS (
                    SELECT CatId, CourseOfferingId, CategoryType,
                           ROW_NUMBER() OVER (
                               PARTITION BY CourseOfferingId, CategoryType 
                               ORDER BY GradeCount DESC, AssCount DESC, CreatedAt ASC
                           ) as rn
                    FROM #CatStats
                )
                SELECT crPrimary.CourseOfferingId, crPrimary.CategoryType,
                       crPrimary.CatId as PrimaryCatId, crDup.CatId as DupCatId
                INTO #CategoryPairs
                FROM CatRanked crPrimary
                JOIN CatRanked crDup ON crPrimary.CourseOfferingId = crDup.CourseOfferingId 
                                    AND crPrimary.CategoryType = crDup.CategoryType
                WHERE crPrimary.rn = 1 AND crDup.rn > 1;

                -- 2. Map assessments under duplicate categories to primary categories
                IF OBJECT_ID('tempdb..#AssPairs') IS NOT NULL DROP TABLE #AssPairs;
                SELECT 
                    cp.CourseOfferingId,
                    cp.CategoryType,
                    aPrimary.Id as PrimaryAssId,
                    aDup.Id as DupAssId
                INTO #AssPairs
                FROM #CategoryPairs cp
                JOIN Assessments aDup ON aDup.AssessmentCategoryId = cp.DupCatId
                JOIN Assessments aPrimary ON aPrimary.AssessmentCategoryId = cp.PrimaryCatId;

                -- Delete duplicate grades where a grade already exists in PrimaryAssId for the same student
                DELETE gDup
                FROM Grades gDup
                JOIN #AssPairs ap ON gDup.AssessmentId = ap.DupAssId
                WHERE EXISTS (
                    SELECT 1 FROM Grades gPri 
                    WHERE gPri.AssessmentId = ap.PrimaryAssId AND gPri.StudentId = gDup.StudentId
                );

                -- Move remaining non-duplicate grades from DupAssId to PrimaryAssId
                UPDATE gDup
                SET gDup.AssessmentId = ap.PrimaryAssId
                FROM Grades gDup
                JOIN #AssPairs ap ON gDup.AssessmentId = ap.DupAssId;

                -- Delete duplicate assessments that have a corresponding primary assessment
                DELETE aDup
                FROM Assessments aDup
                JOIN #AssPairs ap ON aDup.Id = ap.DupAssId;

                -- For any assessments on duplicate categories where NO primary assessment existed, re-parent to primary category
                UPDATE a
                SET a.AssessmentCategoryId = cp.PrimaryCatId
                FROM Assessments a
                JOIN #CategoryPairs cp ON a.AssessmentCategoryId = cp.DupCatId;

                -- 3. Delete duplicate categories
                DELETE ac
                FROM AssessmentCategories ac
                JOIN #CategoryPairs cp ON ac.Id = cp.DupCatId;

                -- 4. Normalize weights on primary categories to standard defaults (10, 10, 10, 70) if needed
                UPDATE ac
                SET ac.Weight = CASE ac.CategoryType 
                                  WHEN 0 THEN 10.00 
                                  WHEN 1 THEN 10.00 
                                  WHEN 2 THEN 10.00 
                                  WHEN 3 THEN 70.00 
                                  ELSE ac.Weight 
                                END,
                    ac.MaxMarks = CASE ac.CategoryType 
                                  WHEN 0 THEN 10.00 
                                  WHEN 1 THEN 10.00 
                                  WHEN 2 THEN 10.00 
                                  WHEN 3 THEN 70.00 
                                  ELSE ac.MaxMarks 
                                END
                FROM AssessmentCategories ac
                JOIN (SELECT DISTINCT CourseOfferingId FROM #CategoryPairs) aff ON ac.CourseOfferingId = aff.CourseOfferingId
                WHERE ac.CategoryType IN (0, 1, 2, 3);

                -- 5. Create unique filtered index if not already exists
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_AssessmentCategories_CourseOffering_CategoryType' AND object_id = OBJECT_ID('AssessmentCategories'))
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX UQ_AssessmentCategories_CourseOffering_CategoryType 
                    ON AssessmentCategories (CourseOfferingId, CategoryType) 
                    WHERE CategoryType IN (0, 1, 2, 3);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_AssessmentCategories_CourseOffering_CategoryType' AND object_id = OBJECT_ID('AssessmentCategories'))
                BEGIN
                    DROP INDEX UQ_AssessmentCategories_CourseOffering_CategoryType ON AssessmentCategories;
                END;
                """);
        }
    }
}
