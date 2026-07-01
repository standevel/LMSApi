using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CourseService(
    ICourseRepository courseRepository,
    IUserRepository userRepository,
    IAuditService auditService,
    LmsDbContext dbContext,
    IFileStorageService fileStorageService,
    INotificationService notificationService) : BaseService(auditService), ICourseService
{
    public async Task<ErrorOr<CourseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course is null) return DomainErrors.Course.NotFound;

        return course.ToDto();
    }

    public async Task<ErrorOr<List<CourseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var courses = await courseRepository.GetAllAsync(ct);
        return courses.Select(c => c.ToDto()).ToList();
    }

    public async Task<ErrorOr<CourseDto>> CreateAsync(CreateCourseRequest request, CancellationToken ct = default)
    {
        var course = new Course
        {
            ProgramId = request.ProgramId,
            Code = request.Code,
            Title = request.Title,
            Description = request.Description,
            CreditUnits = request.CreditUnits,
            LevelId = request.LevelId,
            Semester = request.Semester,
            IsActive = true,
            Offerings = request.Offerings.Select(o => new CourseOffering
            {
                ProgramId = o.ProgramId,
                LevelId = o.LevelId,
                AcademicSessionId = o.AcademicSessionId,
                LecturerId = o.LecturerId,
                Semester = (LMS.Api.Data.Enums.Semester)o.Semester
            }).ToList()
        };

        await courseRepository.AddAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Course", course.Id.ToString(), $"Created course: {course.Code} - {course.Title}", ct);

        var createdCourse = await courseRepository.GetByIdAsync(course.Id, ct);
        return createdCourse!.ToDto();
    }

    public async Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        course.Code = request.Code;
        course.Title = request.Title;
        course.Description = request.Description;
        course.CreditUnits = request.CreditUnits;
        course.LevelId = request.LevelId;
        course.Semester = request.Semester;

        course.Offerings.Clear();
        foreach (var o in request.Offerings)
        {
            course.Offerings.Add(new CourseOffering
            {
                CourseId = id,
                ProgramId = o.ProgramId,
                LevelId = o.LevelId,
                AcademicSessionId = o.AcademicSessionId,
                LecturerId = o.LecturerId,
                Semester = (LMS.Api.Data.Enums.Semester)o.Semester
            });
        }

        await courseRepository.UpdateAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Course", id.ToString(), $"Updated course: {course.Code}", ct);

        var updatedCourse = await courseRepository.GetByIdAsync(id, ct);
        return updatedCourse!.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        await courseRepository.DeleteAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Course", id.ToString(), $"Deleted course: {course.Code}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<CourseDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        course.IsActive = !course.IsActive;
        await courseRepository.UpdateAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleStatus", "Course", id.ToString(), $"Toggled status for course {course.Code} to {course.IsActive}", ct);

        return course.ToDto();
    }

    public async Task<ErrorOr<List<SimpleUserDto>>> GetLecturersAsync(CancellationToken ct = default)
    {
        var lecturers = await userRepository.GetByRoleAsync("Lecturer", ct);
        return lecturers.Select(u => new SimpleUserDto(u.Id, u.DisplayName, u.Email, u.DepartmentId, u.Department?.Name)).ToList();
    }

    public async Task<ErrorOr<LecturerCoursesResponse>> GetMyCoursesAsync(Guid lecturerId, bool isAdmin = false, CancellationToken ct = default)
    {
        // Admins see all offerings; lecturers see only their own
        var query = dbContext.CourseOfferings
            .AsNoTracking()
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .OrderBy(co => co.Course.Code);

        var lecturerIdStr = lecturerId.ToString();
        var offerings = isAdmin
            ? await query.ToListAsync(ct)
            : await query.Where(co => co.LecturerId == lecturerId ||
                                     dbContext.LectureTimetableSlots.Any(slot =>
                                         slot.CourseOfferingId == co.Id &&
                                         (slot.LecturerId == lecturerId ||
                                          (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(lecturerIdStr)))))
                         .ToListAsync(ct);

        if (!offerings.Any())
        {
            return new LecturerCoursesResponse(
                new List<LecturerCourseOfferingDto>(),
                0,
                0);
        }

        // Get student counts for each offering
        var offeringDtos = new List<LecturerCourseOfferingDto>();
        int totalStudents = 0;

        foreach (var offering in offerings)
        {
            var studentCount = await dbContext.CourseEnrollments
                .CountAsync(e => e.CourseOfferingId == offering.Id && e.Status == "Registered", ct);

            // Count upcoming lecture sessions for this course offering
            var sessionCount = await dbContext.LectureSessions
                .CountAsync(ls => ls.CourseOfferingId == offering.Id
                    && ls.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow), ct);

            totalStudents += studentCount;

            offeringDtos.Add(new LecturerCourseOfferingDto(
                offering.Id,
                offering.CourseId,
                offering.Course.Code,
                offering.Course.Title,
                offering.Course.CreditUnits,
                offering.ProgramId,
                offering.Program.Name,
                offering.LevelId,
                offering.Level.Name,
                offering.AcademicSessionId,
                offering.AcademicSession.Name,
                (int)offering.Semester,
                studentCount,
                sessionCount));
        }

        return new LecturerCoursesResponse(
            offeringDtos,
            offeringDtos.Count,
            totalStudents);
    }

    public async Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(Guid offeringId, Guid lecturerId, CancellationToken ct = default)
    {
        // Verify the offering exists and belongs to this lecturer
        var lecturerIdStr = lecturerId.ToString();
        var offering = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == offeringId && 
                (co.LecturerId == lecturerId || 
                 dbContext.LectureTimetableSlots.Any(slot => 
                     slot.CourseOfferingId == co.Id && 
                     (slot.LecturerId == lecturerId || 
                      (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(lecturerIdStr))))), ct);

        if (offering == null)
        {
            return Error.NotFound("Course.NotFound", "Course offering not found or you don't have access to it.");
        }

        // Get materials for this offering
        var materials = await dbContext.CourseMaterials
            .AsNoTracking()
            .Where(cm => cm.CourseOfferingId == offeringId)
            .Include(cm => cm.UploadedBy)
            .OrderByDescending(cm => cm.UploadedAt)
            .Select(cm => new CourseMaterialDto(
                cm.Id,
                cm.Title,
                cm.Description,
                cm.FileUrl,
                cm.FileType,
                cm.FileSize,
                cm.UploadedAt,
                cm.UploadedBy.DisplayName ?? cm.UploadedBy.Email ?? "Unknown"))
            .ToListAsync(ct);

        var enrollments = await dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
            .Include(e => e.Student)
            .OrderBy(e => e.Student.DisplayName)
            .ToListAsync(ct);

        var students = enrollments.Select(e => new CourseStudentDto(
            e.Student.Id,
            e.Student.Id.ToString().Substring(0, 8),
            e.Student.DisplayName ?? e.Student.Email ?? "Unknown",
            e.Student.Email ?? "N/A",
            e.RegisteredAtUtc,
            null)).ToList();

        return new CourseDetailResponse(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Course.Description,
            offering.Course.CreditUnits,
            offering.ProgramId,
            offering.Program.Name,
            offering.LevelId,
            offering.Level.Name,
            offering.AcademicSessionId,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            materials,
            students,
            materials.Count,
            students.Count);
    }

    public async Task<ErrorOr<AddCourseMaterialResponse>> AddCourseMaterialAsync(Guid offeringId, Guid lecturerId, AddCourseMaterialRequest request, CancellationToken ct = default)
    {
        // Verify the offering exists and belongs to this lecturer
        var lecturerIdStr = lecturerId.ToString();
        var offering = await dbContext.CourseOfferings
            .Include(co => co.Course)
            .FirstOrDefaultAsync(co => co.Id == offeringId && 
                (co.LecturerId == lecturerId || 
                 dbContext.LectureTimetableSlots.Any(slot => 
                     slot.CourseOfferingId == co.Id && 
                     (slot.LecturerId == lecturerId || 
                      (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(lecturerIdStr))))), ct);

        if (offering == null)
        {
            return Error.NotFound("Course.NotFound", "Course offering not found or you don't have access to it.");
        }

        string fileUrl;
        string? fileType;
        long? fileSize;

        if (!string.IsNullOrWhiteSpace(request.LinkUrl))
        {
            fileUrl = request.LinkUrl.Trim();
            fileType = "Link";
            fileSize = null;
        }
        else
        {
            if (request.File == null || request.File.Length == 0)
            {
                return Error.Validation("File.Required", "Please select a file or enter a link URL.");
            }

            // Upload file using FileStorageService
            var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
            fileUrl = await fileStorageService.UploadFileAsync(
                request.File,
                $"course-materials/{offeringId}",
                fileName);
            fileType = request.File.ContentType;
            fileSize = request.File.Length;
        }

        var material = new CourseMaterial
        {
            CourseOfferingId = offeringId,
            Title = request.Title,
            Description = request.Description,
            FileUrl = fileUrl,
            FileType = fileType,
            FileSize = fileSize,
            UploadedById = lecturerId,
            UploadedAt = DateTime.UtcNow
        };

        dbContext.CourseMaterials.Add(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("AddMaterial", "CourseMaterial", material.Id.ToString(), 
            $"Added material '{request.Title}' to course {offering.Course.Code}", ct);

        // Trigger notification to enrolled students
        var enrolledStudentIds = await dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
            .Select(e => e.StudentId)
            .ToListAsync(ct);

        foreach (var studentId in enrolledStudentIds)
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                studentId,
                lecturerId,
                $"New Material: {request.Title}",
                $"New course material has been added to {offering.Course.Code}.",
                "System",
                $"/courses/{offeringId}/materials"
            ), ct);
        }

        return new AddCourseMaterialResponse(
            material.Id,
            material.Title,
            material.FileUrl,
            material.UploadedAt);
    }

    public async Task<ErrorOr<Deleted>> DeleteCourseMaterialAsync(Guid materialId, Guid lecturerId, CancellationToken ct = default)
    {
        var material = await dbContext.CourseMaterials
            .Include(cm => cm.CourseOffering)
            .Include(cm => cm.CourseOffering.Course)
            .FirstOrDefaultAsync(cm => cm.Id == materialId, ct);

        if (material == null)
        {
            return Error.NotFound("Material.NotFound", "Material not found.");
        }

        // Verify the lecturer owns this course (primary or via timetable slot)
        var lecturerIdStr = lecturerId.ToString();
        var isAssigned = material.CourseOffering.LecturerId == lecturerId ||
                         await dbContext.LectureTimetableSlots.AnyAsync(slot => 
                             slot.CourseOfferingId == material.CourseOfferingId && 
                             (slot.LecturerId == lecturerId || 
                              (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(lecturerIdStr))), ct);

        if (!isAssigned)
        {
            return Error.Forbidden("Material.Forbidden", "You don't have permission to delete this material.");
        }

        // Delete file from storage
        if (material.FileType != "Link")
        {
            await fileStorageService.DeleteFileAsync(material.FileUrl);
        }

        dbContext.CourseMaterials.Remove(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("DeleteMaterial", "CourseMaterial", materialId.ToString(), 
            $"Deleted material '{material.Title}' from course {material.CourseOffering.Course.Code}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<StudentCourseDetailResponse>> GetStudentCourseDetailAsync(
        Guid offeringId,
        Guid studentId,
        CancellationToken ct = default)
    {
        // 1. Verify the student is enrolled
        var enrollment = await dbContext.CourseEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseOfferingId == offeringId
                                   && e.StudentId == studentId
                                   && e.Status == "Registered", ct);

        if (enrollment == null)
            return Error.Forbidden("Enrollment.Forbidden", "You are not enrolled in this course.");

        // 2. Load offering details
        var offering = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found.");

        // 3. Materials (read-only for student)
        var materials = await dbContext.CourseMaterials
            .AsNoTracking()
            .Where(cm => cm.CourseOfferingId == offeringId)
            .Include(cm => cm.UploadedBy)
            .OrderByDescending(cm => cm.UploadedAt)
            .Select(cm => new CourseMaterialDto(
                cm.Id,
                cm.Title,
                cm.Description,
                cm.FileUrl,
                cm.FileType,
                cm.FileSize,
                cm.UploadedAt,
                cm.UploadedBy.DisplayName ?? cm.UploadedBy.Email ?? "Unknown"))
            .ToListAsync(ct);

        // 4. Check if grades are published for this offering
        var publication = await dbContext.GradePublications
            .AsNoTracking()
            .FirstOrDefaultAsync(gp => gp.CourseOfferingId == offeringId && gp.IsVisibleToStudents, ct);

        bool isPublished = publication != null;

        // 5. Compute student grade from Assessment + Grade tables
        StudentCourseGradeDto? gradeDto = null;
        if (isPublished)
        {
            // Load assessments for the offering with the student's grades
            var assessments = await dbContext.Assessments
                .AsNoTracking()
                .Where(a => a.CourseOfferingId == offeringId)
                .Include(a => a.AssessmentCategory)
                .Include(a => a.Grades.Where(g => g.StudentId == studentId))
                .ToListAsync(ct);

            if (assessments.Any())
            {
                // Group by category type to compute per-category weighted scores
                double ca1 = 0, ca2 = 0, ca3 = 0, exam = 0;
                double total = 0;

                foreach (var assessment in assessments)
                {
                    var studentGrade = assessment.Grades.FirstOrDefault();
                    if (studentGrade == null) continue;

                    double maxMarks = (double)assessment.MaxMarks;
                    double obtained = (double)studentGrade.MarksObtained;
                    double weight = (double)assessment.AssessmentCategory.Weight;
                    double weighted = maxMarks > 0 ? (obtained / maxMarks) * weight : 0;

                    var catType = assessment.AssessmentCategory.CategoryType;
                    if (catType == AssessmentCategoryType.CA1) ca1 += weighted;
                    else if (catType == AssessmentCategoryType.CA2) ca2 += weighted;
                    else if (catType == AssessmentCategoryType.CA3) ca3 += weighted;
                    else if (assessment.AssessmentCategory.IsExamCategory) exam += weighted;

                    total += weighted;
                }

                var sysConfig = await dbContext.SystemGradingConfigurations
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .FirstOrDefaultAsync(ct);

                var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
                    ? new List<LMS.Api.Contracts.GradeMappingDto>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson) 
                      ?? new List<LMS.Api.Contracts.GradeMappingDto>();

                string letterGrade = "F";
                double gradePoints = 0.0;
                
                if (mappings == null || !mappings.Any())
                {
                    // Simple letter grade mapping (5-point scale) fallback
                    letterGrade = total >= 70 ? "A"
                        : total >= 60 ? "B"
                        : total >= 50 ? "C"
                        : total >= 45 ? "D"
                        : total >= 40 ? "E"
                        : "F";

                    gradePoints = letterGrade switch
                    {
                        "A" => 5.0,
                        "B" => 4.0,
                        "C" => 3.0,
                        "D" => 2.0,
                        "E" => 1.0,
                        _ => 0.0
                    };
                }
                else
                {
                    var match = mappings.OrderByDescending(m => m.MinPercentage)
                        .FirstOrDefault(m => (decimal)total >= m.MinPercentage);
                        
                    letterGrade = match?.LetterGrade ?? "F";
                    gradePoints = match != null ? (double)match.GradePoints : 0.0;
                }

                gradeDto = new StudentCourseGradeDto(
                    Math.Round(ca1, 2),
                    Math.Round(ca2, 2),
                    Math.Round(ca3, 2),
                    Math.Round(exam, 2),
                    Math.Round(total, 2),
                    letterGrade,
                    gradePoints,
                    true);
            }
        }

        // 6. Class analytics: score distribution across all enrolled students (published only)
        CourseClassAnalyticsDto? analyticsDto = null;
        if (isPublished)
        {
            // Get all enrolled student IDs for this offering
            var enrolledStudentIds = await dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
                .Select(e => e.StudentId)
                .ToListAsync(ct);

            if (enrolledStudentIds.Count > 1)
            {
                // Compute total scores for each enrolled student
                var allAssessments = await dbContext.Assessments
                    .AsNoTracking()
                    .Where(a => a.CourseOfferingId == offeringId)
                    .Include(a => a.AssessmentCategory)
                    .Include(a => a.Grades.Where(g => enrolledStudentIds.Contains(g.StudentId)))
                    .ToListAsync(ct);

                // Aggregate per student
                var studentTotals = new Dictionary<Guid, double>();
                foreach (var sid in enrolledStudentIds)
                {
                    double t = 0;
                    foreach (var assessment in allAssessments)
                    {
                        var g = assessment.Grades.FirstOrDefault(gr => gr.StudentId == sid);
                        if (g == null) continue;
                        double maxM = (double)assessment.MaxMarks;
                        double w = (double)assessment.AssessmentCategory.Weight;
                        if (maxM > 0) t += ((double)g.MarksObtained / maxM) * w;
                    }
                    studentTotals[sid] = Math.Round(t, 1);
                }

                var scores = studentTotals.Values.ToList();
                if (scores.Count > 0)
                {
                    double classAverage = scores.Average();
                    double? myScore = studentTotals.TryGetValue(studentId, out var ms) ? ms : null;

                    var buckets = new List<ScoreBucketDto>();
                    for (int start = 0; start < 100; start += 10)
                    {
                        int end = start == 90 ? 100 : start + 9;
                        int count = scores.Count(s => s >= start && s <= end);
                        buckets.Add(new ScoreBucketDto(start, end, count));
                    }

                    int? percentile = null;
                    if (myScore.HasValue)
                    {
                        int below = scores.Count(s => s < myScore.Value);
                        percentile = (int)Math.Round((double)below / scores.Count * 100);
                    }

                    analyticsDto = new CourseClassAnalyticsDto(
                        Math.Round(classAverage, 1),
                        myScore,
                        percentile,
                        scores.Count,
                        buckets);
                }
            }
        }

        return new StudentCourseDetailResponse(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Course.Description,
            offering.Course.CreditUnits,
            offering.Program.Name,
            offering.Level.Name,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            materials,
            materials.Count,
            gradeDto,
            analyticsDto);
    }
}

