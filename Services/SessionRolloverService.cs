using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class SessionRolloverService : BaseService, ISessionRolloverService
{
    private readonly LmsDbContext dbContext;
    private readonly IFeeService feeService;
    private readonly IGpaCalculationService gpaCalculationService;
    private readonly ILogger<SessionRolloverService> logger;

    public SessionRolloverService(
        LmsDbContext dbContext,
        IFeeService feeService,
        IGpaCalculationService gpaCalculationService,
        IAuditService auditService,
        ILogger<SessionRolloverService> logger) : base(auditService)
    {
        this.dbContext = dbContext;
        this.feeService = feeService;
        this.gpaCalculationService = gpaCalculationService;
        this.logger = logger;
    }

    public async Task<ErrorOr<SessionRolloverResultDto>> RolloverSessionAsync(SessionRolloverRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.SourceSessionId == request.TargetSessionId)
        {
            return Error.Validation("Rollover.SameSession", "Source and Target sessions must be different.");
        }

        var sourceSession = await dbContext.AcademicSessions.FindAsync([request.SourceSessionId], ct);
        if (sourceSession == null)
        {
            return Error.NotFound("Rollover.SourceSessionNotFound", "Source academic session not found.");
        }

        var targetSession = await dbContext.AcademicSessions.FindAsync([request.TargetSessionId], ct);
        if (targetSession == null)
        {
            return Error.NotFound("Rollover.TargetSessionNotFound", "Target academic session not found.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<ErrorOr<SessionRolloverResultDto>>(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

            int coursesRolledOver = 0;
            int lecturersAssigned = 0;
            int timetableSlotsCopied = 0;
            int feeTemplatesCloned = 0;
            int feeAssignmentsCopied = 0;
            int scholarshipsRolledOver = 0;
            int curriculumsCloned = 0;
            int studentsPromoted = 0;
            int studentsNotPromoted = 0;
            var logs = new List<string>();

            try
            {
                logs.Add($"Initiating rollover from session '{sourceSession.Name}' to '{targetSession.Name}'...");

                // 1. Course Offerings Rollover
                var offeringMap = new Dictionary<Guid, Guid>(); // Map old offering ID -> new offering ID
                if (request.RollOverCourses)
                {
                    logs.Add("Rolling over course offerings...");
                    var sourceOfferings = await dbContext.CourseOfferings
                        .Include(co => co.Programs)
                        .Include(co => co.Lecturers)
                        .Where(co => co.AcademicSessionId == request.SourceSessionId)
                        .ToListAsync(ct);

                    foreach (var sourceOffering in sourceOfferings)
                    {
                        // Check if offering already exists in target session
                        var targetOffering = await dbContext.CourseOfferings
                            .FirstOrDefaultAsync(co => co.CourseId == sourceOffering.CourseId 
                                                       && co.AcademicSessionId == request.TargetSessionId 
                                                       && co.Semester == sourceOffering.Semester, ct);

                        bool isNewOffering = false;
                        if (targetOffering == null)
                        {
                            targetOffering = new CourseOffering
                            {
                                Id = Guid.NewGuid(),
                                CourseId = sourceOffering.CourseId,
                                AcademicSessionId = request.TargetSessionId,
                                Semester = sourceOffering.Semester,
                                CurriculumId = sourceOffering.CurriculumId
                            };
                            dbContext.CourseOfferings.Add(targetOffering);
                            coursesRolledOver++;
                            isNewOffering = true;
                        }

                        offeringMap[sourceOffering.Id] = targetOffering.Id;

                        // Roll over program/level links
                        foreach (var prog in sourceOffering.Programs)
                        {
                            var exists = !isNewOffering && await dbContext.CourseOfferingPrograms
                                .AnyAsync(cop => cop.CourseOfferingId == targetOffering.Id 
                                                 && cop.ProgramId == prog.ProgramId 
                                                 && cop.LevelId == prog.LevelId, ct);
                            if (!exists)
                            {
                                dbContext.CourseOfferingPrograms.Add(new CourseOfferingProgram
                                {
                                    Id = Guid.NewGuid(),
                                    CourseOfferingId = targetOffering.Id,
                                    ProgramId = prog.ProgramId,
                                    LevelId = prog.LevelId
                                });
                            }
                        }

                        // Roll over lecturers
                        if (request.RollOverLecturers)
                        {
                            foreach (var lec in sourceOffering.Lecturers)
                            {
                                var exists = !isNewOffering && await dbContext.CourseOfferingLecturers
                                    .AnyAsync(col => col.CourseOfferingId == targetOffering.Id 
                                                     && col.LecturerId == lec.LecturerId, ct);
                                if (!exists)
                                {
                                    dbContext.CourseOfferingLecturers.Add(new CourseOfferingLecturer
                                    {
                                        Id = Guid.NewGuid(),
                                        CourseOfferingId = targetOffering.Id,
                                        LecturerId = lec.LecturerId,
                                        Role = lec.Role
                                    });
                                    lecturersAssigned++;
                                }
                            }
                        }
                    }

                    await dbContext.SaveChangesAsync(ct);
                    logs.Add($"Rolled over {coursesRolledOver} course offerings and assigned {lecturersAssigned} lecturers.");
                }

                // 2. Timetable Rollover
                if (request.RollOverTimetable && request.RollOverCourses)
                {
                    logs.Add("Rolling over timetable schedules...");
                    var sourceOfferingIds = offeringMap.Keys.ToList();
                    var timetableSlots = await dbContext.LectureTimetableSlots
                        .Where(ts => sourceOfferingIds.Contains(ts.CourseOfferingId))
                        .ToListAsync(ct);

                    foreach (var slot in timetableSlots)
                    {
                        if (offeringMap.TryGetValue(slot.CourseOfferingId, out var targetOfferingId))
                        {
                            var exists = await dbContext.LectureTimetableSlots
                                .AnyAsync(ts => ts.CourseOfferingId == targetOfferingId 
                                                 && ts.DayOfWeek == slot.DayOfWeek 
                                                 && ts.StartTime == slot.StartTime 
                                                 && ts.EndTime == slot.EndTime 
                                                 && ts.VenueId == slot.VenueId, ct);
                            if (!exists)
                            {
                                dbContext.LectureTimetableSlots.Add(new LectureTimetableSlot
                                {
                                    Id = Guid.NewGuid(),
                                    CourseOfferingId = targetOfferingId,
                                    LecturerId = slot.LecturerId,
                                    CoLecturersJson = slot.CoLecturersJson,
                                    VenueId = slot.VenueId,
                                    DayOfWeek = slot.DayOfWeek,
                                    StartTime = slot.StartTime,
                                    EndTime = slot.EndTime,
                                    DurationMinutes = slot.DurationMinutes,
                                    Notes = slot.Notes,
                                    CreatedByUser = await dbContext.Users.FindAsync([userId], ct) ?? slot.CreatedByUser,
                                    CreatedByUserId = userId,
                                    CreatedBy = userId
                                });
                                timetableSlotsCopied++;
                            }
                        }
                    }

                    await dbContext.SaveChangesAsync(ct);
                    logs.Add($"Copied {timetableSlotsCopied} weekly timetable slots.");
                }

                // 3. Financials Rollover (Fee templates & assignments)
                if (request.RollOverFinancials)
                {
                    logs.Add("Cloning session-specific fee templates & assignments...");
                    var sourceTemplates = await dbContext.FeeTemplates
                        .Include(t => t.LineItems)
                        .Where(t => t.SessionId == request.SourceSessionId && t.IsActive)
                        .ToListAsync(ct);

                    var templateMap = new Dictionary<Guid, Guid>(); // old template ID -> new template ID

                    foreach (var template in sourceTemplates)
                    {
                        var targetTemplateName = $"{template.Name} ({targetSession.Name})";
                        var targetTemplate = await dbContext.FeeTemplates
                            .FirstOrDefaultAsync(t => t.SessionId == request.TargetSessionId 
                                                       && t.Name == targetTemplateName, ct);

                        if (targetTemplate == null)
                        {
                            targetTemplate = new FeeTemplate
                            {
                                Id = Guid.NewGuid(),
                                Name = targetTemplateName,
                                Description = template.Description,
                                FeeCategoryId = template.FeeCategoryId,
                                Scope = template.Scope,
                                SessionId = request.TargetSessionId,
                                FacultyId = template.FacultyId,
                                ProgramId = template.ProgramId,
                                DueDate = template.DueDate.HasValue ? targetSession.EndDate : (DateTime?)null,
                                LateFeeType = template.LateFeeType,
                                LateFeeAmount = template.LateFeeAmount,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                LineItems = template.LineItems.Select(li => new FeeLineItem
                                {
                                    Id = Guid.NewGuid(),
                                    Name = li.Name,
                                    Description = li.Description,
                                    Amount = li.Amount,
                                    ExchangeRate = li.ExchangeRate
                                }).ToList()
                            };

                            dbContext.FeeTemplates.Add(targetTemplate);
                            feeTemplatesCloned++;
                        }

                        templateMap[template.Id] = targetTemplate.Id;
                    }

                    await dbContext.SaveChangesAsync(ct);

                    // Rollover Fee Assignments
                    var sourceAssignments = await dbContext.FeeAssignments
                        .Where(a => a.SessionId == request.SourceSessionId && a.IsActive)
                        .ToListAsync(ct);

                    foreach (var ass in sourceAssignments)
                    {
                        var templateId = ass.FeeTemplateId;
                        // Use cloned template if it was session-specific, otherwise keep existing template (for global recurring templates)
                        if (templateMap.TryGetValue(ass.FeeTemplateId, out var clonedTemplateId))
                        {
                            templateId = clonedTemplateId;
                        }

                        var exists = await dbContext.FeeAssignments
                            .AnyAsync(a => a.SessionId == request.TargetSessionId 
                                             && a.FeeTemplateId == templateId 
                                             && a.ProgramId == ass.ProgramId 
                                             && a.FacultyId == ass.FacultyId 
                                             && a.StudentId == ass.StudentId, ct);
                        if (!exists)
                        {
                            dbContext.FeeAssignments.Add(new FeeAssignment
                            {
                                Id = Guid.NewGuid(),
                                FeeTemplateId = templateId,
                                Scope = ass.Scope,
                                FacultyId = ass.FacultyId,
                                ProgramId = ass.ProgramId,
                                StudentId = ass.StudentId,
                                SessionId = request.TargetSessionId,
                                AmountOverride = ass.AmountOverride,
                                DueDateOverride = ass.DueDateOverride,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            });
                            feeAssignmentsCopied++;
                        }
                    }

                    await dbContext.SaveChangesAsync(ct);
                    logs.Add($"Cloned {feeTemplatesCloned} fee templates and copied {feeAssignmentsCopied} assignments.");
                }

                // 4. Scholarship Rollover
                if (request.RollOverScholarships)
                {
                    logs.Add("Rolling over student scholarships...");
                    var sourceScholarships = await dbContext.StudentScholarships
                        .Where(s => s.SessionId == request.SourceSessionId)
                        .ToListAsync(ct);

                    foreach (var ss in sourceScholarships)
                    {
                        var exists = await dbContext.StudentScholarships
                            .AnyAsync(s => s.SessionId == request.TargetSessionId 
                                             && s.StudentId == ss.StudentId 
                                             && s.ScholarshipId == ss.ScholarshipId, ct);
                        if (!exists)
                        {
                            dbContext.StudentScholarships.Add(new StudentScholarship
                            {
                                Id = Guid.NewGuid(),
                                StudentId = ss.StudentId,
                                ScholarshipId = ss.ScholarshipId,
                                SessionId = request.TargetSessionId,
                                CalculatedAmount = ss.CalculatedAmount,
                                CreatedAt = DateTime.UtcNow
                            });
                            scholarshipsRolledOver++;
                        }
                    }

                    await dbContext.SaveChangesAsync(ct);
                    logs.Add($"Rolled over {scholarshipsRolledOver} active student scholarships.");
                }

                // 5. Curriculum Rollover
                if (request.CloneCurriculums)
                {
                    logs.Add("Cloning published curriculums for new intake...");
                    var sourceCurricula = await dbContext.Curricula
                        .Include(c => c.Courses)
                        .Where(c => c.AdmissionSessionId == request.SourceSessionId && c.Status == CurriculumStatus.Published)
                        .ToListAsync(ct);

                    foreach (var curr in sourceCurricula)
                    {
                        var targetName = $"{curr.Name} ({targetSession.Name} Intake)";
                        var exists = await dbContext.Curricula
                            .AnyAsync(c => c.AdmissionSessionId == request.TargetSessionId 
                                             && c.Name == targetName, ct);
                        if (!exists)
                        {
                            var clonedCurr = new Curriculum
                            {
                                Id = Guid.NewGuid(),
                                ProgramId = curr.ProgramId,
                                AdmissionSessionId = request.TargetSessionId,
                                Name = targetName,
                                MinCreditUnitsForGraduation = curr.MinCreditUnitsForGraduation,
                                Status = CurriculumStatus.Draft,
                                ParentCurriculumId = curr.Id,
                                IsActive = true,
                                CreatedUtc = DateTime.UtcNow,
                                Courses = curr.Courses.Select(c => new CurriculumCourse
                                {
                                    Id = Guid.NewGuid(),
                                    LevelId = c.LevelId,
                                    CourseId = c.CourseId,
                                    Semester = c.Semester,
                                    Category = c.Category,
                                    CreditUnits = c.CreditUnits
                                }).ToList()
                            };

                            dbContext.Curricula.Add(clonedCurr);
                            curriculumsCloned++;
                        }
                    }

                    await dbContext.SaveChangesAsync(ct);
                    logs.Add($"Cloned {curriculumsCloned} curriculums as drafts in target session.");
                }

                // 6. Set active session if requested
                if (request.MakeTargetSessionActive)
                {
                    logs.Add($"Setting target session '{targetSession.Name}' as the active session...");
                    var otherSessions = await dbContext.AcademicSessions
                        .Where(s => s.Id != targetSession.Id && s.IsActive)
                        .ToListAsync(ct);
                    foreach (var s in otherSessions)
                    {
                        s.IsActive = false;
                    }
                    targetSession.IsActive = true;
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                logs.Add("Base session metadata rollover committed successfully.");

                // 7. Student Promotion (Processed in batches)
                if (request.PromoteStudents)
                {
                    logs.Add("Retrieving active students for promotion...");
                    var studentIds = await dbContext.Students
                        .Where(s => s.AcademicSessionId == request.SourceSessionId && s.Status == StudentStatus.Active)
                        .Select(s => s.Id)
                        .ToListAsync(ct);

                    logs.Add($"Found {studentIds.Count} active students to process.");

                    int batchSize = 100;
                    for (int i = 0; i < studentIds.Count; i += batchSize)
                    {
                        var batchIds = studentIds.Skip(i).Take(batchSize).ToList();
                        await using var batchTx = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
                        try
                        {
                            var studentsBatch = await dbContext.Students
                                .Include(s => s.Level)
                                .Where(s => batchIds.Contains(s.Id))
                                .ToListAsync(ct);

                            foreach (var student in studentsBatch)
                            {
                                if (student.AcademicProgramId == null || student.LevelId == null)
                                {
                                    logs.Add($"Skipping student {student.FirstName} {student.LastName} - no program or level assigned.");
                                    studentsNotPromoted++;
                                    continue;
                                }

                                // Check academic standing
                                var standing = await dbContext.AcademicStandings
                                    .Where(ast => ast.StudentId == student.Id)
                                    .OrderByDescending(ast => ast.EffectiveDate)
                                    .FirstOrDefaultAsync(ct);

                                if (standing != null)
                                {
                                    if (standing.StandingType == AcademicStandingType.Suspension || 
                                        standing.StandingType == AcademicStandingType.Expulsion)
                                    {
                                        logs.Add($"Skipping student {student.FirstName} {student.LastName} - academic standing: {standing.StandingType}.");
                                        studentsNotPromoted++;
                                        continue;
                                    }
                                    if (request.OnlyPromoteGoodStanding && standing.StandingType == AcademicStandingType.Probation)
                                    {
                                        logs.Add($"Skipping student {student.FirstName} {student.LastName} - on Academic Probation.");
                                        studentsNotPromoted++;
                                        continue;
                                    }
                                }

                                if (request.OnlyPromoteGoodStanding)
                                {
                                    var gpaResult = await gpaCalculationService.GetStudentGpaAsync(student.Id, request.SourceSessionId, ct);
                                    if (!gpaResult.IsError && gpaResult.Value.CumulativeGpa < 1.0m)
                                    {
                                        logs.Add($"Skipping student {student.FirstName} {student.LastName} - low CGPA ({gpaResult.Value.CumulativeGpa:F2}).");
                                        studentsNotPromoted++;
                                        continue;
                                    }
                                }

                                // Query levels for this program ordered by Order
                                var programLevels = await dbContext.Levels
                                    .Where(l => l.ProgramId == student.AcademicProgramId.Value)
                                    .OrderBy(l => l.Order)
                                    .ToListAsync(ct);

                                var currentLevelIndex = programLevels.FindIndex(l => l.Id == student.LevelId.Value);
                                if (currentLevelIndex == -1)
                                {
                                    logs.Add($"Skipping student {student.FirstName} {student.LastName} - current level not found in program levels.");
                                    studentsNotPromoted++;
                                    continue;
                                }

                                if (currentLevelIndex + 1 < programLevels.Count)
                                {
                                    var nextLevel = programLevels[currentLevelIndex + 1];

                                    // Update student level and session
                                    student.LevelId = nextLevel.Id;
                                    student.AcademicSessionId = request.TargetSessionId;
                                    student.UpdatedAt = DateTime.UtcNow;

                                    // Resolve curriculum
                                    var sourceEnrollment = await dbContext.Enrollments
                                        .FirstOrDefaultAsync(e => e.UserId == student.Id && e.AcademicSessionId == request.SourceSessionId, ct);

                                    Guid curriculumId = Guid.Empty;
                                    if (sourceEnrollment != null)
                                    {
                                        curriculumId = sourceEnrollment.CurriculumId;
                                    }
                                    else
                                    {
                                        curriculumId = await dbContext.Curricula
                                            .Where(c => c.ProgramId == student.AcademicProgramId.Value && c.Status == CurriculumStatus.Published)
                                            .Select(c => c.Id)
                                            .FirstOrDefaultAsync(ct);
                                    }

                                    if (curriculumId == Guid.Empty)
                                    {
                                        curriculumId = await dbContext.Curricula
                                            .Where(c => c.ProgramId == student.AcademicProgramId.Value)
                                            .Select(c => c.Id)
                                            .FirstOrDefaultAsync(ct);
                                    }

                                    // Create target ProgramEnrollment
                                    var enrollmentExists = await dbContext.Enrollments
                                        .AnyAsync(e => e.UserId == student.Id && e.AcademicSessionId == request.TargetSessionId && e.LevelId == nextLevel.Id, ct);

                                    if (!enrollmentExists && curriculumId != Guid.Empty)
                                    {
                                        var newEnrollment = new ProgramEnrollment
                                        {
                                            Id = Guid.NewGuid(),
                                            ProgramId = student.AcademicProgramId.Value,
                                            LevelId = nextLevel.Id,
                                            UserId = student.Id,
                                            AcademicSessionId = request.TargetSessionId,
                                            CurriculumId = curriculumId,
                                            EnrolledAtUtc = DateTime.UtcNow
                                        };
                                        dbContext.Enrollments.Add(newEnrollment);
                                    }

                                    // Auto-register core courses
                                    if (request.RollOverCourseRegistrations && curriculumId != Guid.Empty)
                                    {
                                        var curriculum = await dbContext.Curricula
                                            .Include(c => c.Courses)
                                            .FirstOrDefaultAsync(c => c.Id == curriculumId, ct);

                                        if (curriculum != null)
                                        {
                                            var curriculumCourses = curriculum.Courses
                                                .Where(cc => cc.LevelId == nextLevel.Id && cc.Category == CourseCategory.Compulsory)
                                                .ToList();

                                            foreach (var cc in curriculumCourses)
                                            {
                                                var targetOffering = await dbContext.CourseOfferings
                                                    .FirstOrDefaultAsync(co => co.CourseId == cc.CourseId 
                                                                               && co.AcademicSessionId == request.TargetSessionId 
                                                                               && co.Semester == cc.Semester, ct);

                                                if (targetOffering != null)
                                                {
                                                    var regExists = await dbContext.CourseEnrollments
                                                        .AnyAsync(ce => ce.StudentId == student.Id && ce.CourseOfferingId == targetOffering.Id, ct);

                                                    if (!regExists)
                                                    {
                                                        dbContext.CourseEnrollments.Add(new CourseEnrollment
                                                        {
                                                            Id = Guid.NewGuid(),
                                                            StudentId = student.Id,
                                                            CourseOfferingId = targetOffering.Id,
                                                            Status = "Registered",
                                                            RegisteredAtUtc = DateTime.UtcNow,
                                                            CreatedById = userId
                                                        });
                                                        logs.Add($"Auto-registered student {student.FirstName} {student.LastName} for core course offering {targetOffering.Id}.");
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Generate billing
                                    try
                                    {
                                        await feeService.GenerateStudentBillAsync(student.Id, request.TargetSessionId);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogWarning(ex, "Failed to generate bill for student {StudentId} in target session {SessionId}", student.Id, request.TargetSessionId);
                                        logs.Add($"Warning: Bill generation failed for {student.FirstName} {student.LastName}: {ex.Message}");
                                    }

                                    studentsPromoted++;
                                }
                                else
                                {
                                    // Student at max level - check graduation status
                                    var audit = await dbContext.DegreeAudits
                                        .Where(da => da.StudentId == student.Id)
                                        .OrderByDescending(da => da.GeneratedAt)
                                        .FirstOrDefaultAsync(ct);

                                    if (audit != null && audit.Status == DegreeAuditStatus.Complete)
                                    {
                                        student.Status = StudentStatus.Graduated;
                                        student.GraduationDate = DateTime.UtcNow;
                                        student.UpdatedAt = DateTime.UtcNow;
                                        logs.Add($"Student {student.FirstName} {student.LastName} ({student.StudentNumber ?? "No Matric"}) completed degree requirements and is marked as Graduated.");
                                        studentsPromoted++;
                                    }
                                    else
                                    {
                                        studentsNotPromoted++;
                                        logs.Add($"Student {student.FirstName} {student.LastName} ({student.StudentNumber ?? "No Matric"}) reached maximum level ({student.Level?.Name}) but has incomplete degree audit. Flagged for graduation review.");
                                    }
                                }
                            }

                            await dbContext.SaveChangesAsync(ct);
                            await batchTx.CommitAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            await batchTx.RollbackAsync(ct);
                            logs.Add($"Error promoting student batch starting at index {i}: {ex.Message}");
                        }
                    }

                    logs.Add($"Promotion processing finished. Promoted/Graduated: {studentsPromoted}. Skipped/Max level: {studentsNotPromoted}.");
                }

                await LogActionAsync("RolloverSession", "AcademicSession", request.TargetSessionId.ToString(),
                    $"Rollover executed successfully from {sourceSession.Name} to {targetSession.Name}", ct);

                logs.Add("Rollover completed successfully.");

                return new SessionRolloverResultDto
                {
                    CoursesRolledOver = coursesRolledOver,
                    LecturersAssigned = lecturersAssigned,
                    TimetableSlotsCopied = timetableSlotsCopied,
                    FeeTemplatesCloned = feeTemplatesCloned,
                    FeeAssignmentsCopied = feeAssignmentsCopied,
                    ScholarshipsRolledOver = scholarshipsRolledOver,
                    CurriculumsCloned = curriculumsCloned,
                    StudentsPromoted = studentsPromoted,
                    StudentsNotPromoted = studentsNotPromoted,
                    Logs = logs
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Session rollover failed: {Message}", ex.Message);
                await transaction.RollbackAsync(ct);
                logs.Add($"Fatal Error: Rollover rolled back due to error: {ex.Message}");
                return Error.Validation("Rollover.Failed", $"Session rollover failed: {ex.Message}");
            }
        });
    }
}
