                    WITH CTE AS (
                        SELECT 
                            Id, 
                            UPPER(REPLACE(REPLACE(Code, ' ', ''), '-', '')) AS NormalizedCode,
                            ROW_NUMBER() OVER(PARTITION BY UPPER(REPLACE(REPLACE(Code, ' ', ''), '-', '')) ORDER BY Id ASC) as RowNum
                        FROM Courses
                    )
                    SELECT Id AS DuplicateId, 
                           (SELECT Id FROM CTE c2 WHERE c2.NormalizedCode = CTE.NormalizedCode AND c2.RowNum = 1) AS PrimaryId
                    INTO #TempDups
                    FROM CTE 
                    WHERE RowNum > 1;

                    -- Update CurriculumCourses (Ignore conflicts by deleting the duplicate ones before update)
                    DELETE FROM CurriculumCourses 
                    WHERE Id IN (
                        SELECT cc.Id FROM CurriculumCourses cc
                        JOIN #TempDups d ON cc.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CurriculumCourses cc2 
                            WHERE cc2.CourseId = d.PrimaryId 
                              AND cc2.CurriculumId = cc.CurriculumId 
                              AND cc2.Semester = cc.Semester
                        )
                    );
                    UPDATE CurriculumCourses SET CourseId = d.PrimaryId 
                    FROM CurriculumCourses cc JOIN #TempDups d ON cc.CourseId = d.DuplicateId;

                    -- Update CourseOfferings
                    DELETE FROM CourseOfferings 
                    WHERE Id IN (
                        SELECT co.Id FROM CourseOfferings co
                        JOIN #TempDups d ON co.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CourseOfferings co2 
                            WHERE co2.CourseId = d.PrimaryId 
                              AND co2.ProgramId = co.ProgramId 
                              AND co2.LevelId = co.LevelId 
                              AND co2.AcademicSessionId = co.AcademicSessionId
                              AND co2.Semester = co.Semester
                        )
                    );
                    UPDATE CourseOfferings SET CourseId = d.PrimaryId 
                    FROM CourseOfferings co JOIN #TempDups d ON co.CourseId = d.DuplicateId;

                    -- Update DegreeRequirements
                    UPDATE DegreeRequirementCourses SET CourseId = d.PrimaryId 
                    FROM DegreeRequirementCourses dr JOIN #TempDups d ON dr.CourseId = d.DuplicateId;

                    -- Update Assignments
                    UPDATE Assignments SET CourseId = d.PrimaryId 
                    FROM Assignments a JOIN #TempDups d ON a.CourseId = d.DuplicateId;

                    -- Update Prerequisites (Delete duplicate links first)
                    DELETE FROM CoursePrerequisites 
                    WHERE Id IN (
                        SELECT cp.Id FROM CoursePrerequisites cp
                        JOIN #TempDups d ON cp.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CoursePrerequisites cp2 
                            WHERE cp2.CourseId = d.PrimaryId 
                              AND cp2.PrerequisiteCourseId = cp.PrerequisiteCourseId
                        )
                    );
                    UPDATE CoursePrerequisites SET CourseId = d.PrimaryId 
                    FROM CoursePrerequisites cp JOIN #TempDups d ON cp.CourseId = d.DuplicateId;

                    DELETE FROM CoursePrerequisites 
                    WHERE Id IN (
                        SELECT cp.Id FROM CoursePrerequisites cp
                        JOIN #TempDups d ON cp.PrerequisiteCourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CoursePrerequisites cp2 
                            WHERE cp2.PrerequisiteCourseId = d.PrimaryId 
                              AND cp2.CourseId = cp.CourseId
                        )
                    );
                    UPDATE CoursePrerequisites SET PrerequisiteCourseId = d.PrimaryId 
                    FROM CoursePrerequisites cp JOIN #TempDups d ON cp.PrerequisiteCourseId = d.DuplicateId;

                    -- Finally delete duplicate courses
                    DELETE FROM Courses WHERE Id IN (SELECT DuplicateId FROM #TempDups);
                    
                    SELECT COUNT(*) AS DuplicatesRemoved FROM #TempDups;
                    DROP TABLE #TempDups;
