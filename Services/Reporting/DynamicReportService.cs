using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services.Reporting;

public class DynamicReportService : IDynamicReportService
{
    private readonly LmsDbContext _db;
    private readonly ILogger<DynamicReportService> _logger;

    public DynamicReportService(LmsDbContext db, ILogger<DynamicReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<List<ReportDatasetMetadataDto>> GetAvailableDatasetsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ValidateNonStudent(user);
        return Task.FromResult(DynamicReportCatalog.GetDatasetsForUser(user));
    }

    public async Task<DynamicReportResultDto> ExecuteQueryAsync(ClaimsPrincipal user, DynamicReportQueryRequest request, CancellationToken ct = default)
    {
        ValidateNonStudent(user);
        var sw = Stopwatch.StartNew();

        if (!DynamicReportCatalog.CanUserAccessDataset(user, request.DatasetId))
        {
            throw new UnauthorizedAccessException($"User does not have authorization to query dataset '{request.DatasetId}'.");
        }

        var dataset = DynamicReportCatalog.GetAllDatasets().FirstOrDefault(d => d.Id.Equals(request.DatasetId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Dataset '{request.DatasetId}' not found.");

        var allFields = dataset.Tables.SelectMany(t => t.Fields).ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);

        var selectedFieldIds = request.SelectedFields?.Where(f => allFields.ContainsKey(f)).ToList() ?? [];
        if (selectedFieldIds.Count == 0)
        {
            selectedFieldIds = dataset.DefaultSelectedFields.Where(f => allFields.ContainsKey(f)).ToList();
        }

        var columnHeaders = selectedFieldIds.Select(fId =>
        {
            var meta = allFields[fId];
            return new ReportColumnHeaderDto(
                meta.Id,
                meta.DisplayName,
                dataset.Tables.FirstOrDefault(t => t.Id == meta.TableId)?.DisplayName ?? meta.TableId,
                meta.DataType
            );
        }).ToList();

        var rawRows = await FetchDatasetRowsAsync(user, request.DatasetId, ct);
        var filteredRows = ApplyFilters(rawRows, request.Filters, request.SearchTerm, allFields);
        var sortedRows = ApplySorting(filteredRows, request.Sorts, allFields);

        var totalCount = sortedRows.Count;

        List<Dictionary<string, object?>> pagedRows;
        if (request.ExportAll)
        {
            pagedRows = sortedRows;
        }
        else
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 500);
            pagedRows = sortedRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        var projectedRows = pagedRows.Select(row =>
        {
            var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columnHeaders)
            {
                row.TryGetValue(col.FieldId, out var val);
                projected[col.FieldId] = val;
            }
            return projected;
        }).ToList();

        var numericAggregates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnHeaders.Where(c => c.DataType is "number" or "currency"))
        {
            var numbers = filteredRows
                .Select(r => r.TryGetValue(col.FieldId, out var v) && v != null ? (decimal?)ConvertToDecimal(v) : (decimal?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            if (numbers.Count > 0)
            {
                numericAggregates[$"{col.FieldId}_sum"] = Math.Round(numbers.Sum(), 2);
                numericAggregates[$"{col.FieldId}_avg"] = Math.Round(numbers.Average(), 2);
            }
        }

        sw.Stop();

        var totalPages = request.ExportAll ? 1 : (int)Math.Ceiling((double)totalCount / Math.Clamp(request.PageSize, 1, 500));

        return new DynamicReportResultDto(
            request.DatasetId,
            columnHeaders,
            projectedRows,
            totalCount,
            request.ExportAll ? 1 : request.Page,
            request.ExportAll ? totalCount : request.PageSize,
            totalPages,
            numericAggregates,
            sw.ElapsedMilliseconds
        );
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportReportAsync(ClaimsPrincipal user, ExportDynamicReportRequest request, CancellationToken ct = default)
    {
        var queryRequest = request.Query with { ExportAll = true };
        var queryResult = await ExecuteQueryAsync(user, queryRequest, ct);

        var reportTitle = string.IsNullOrWhiteSpace(request.Title)
            ? $"Report_{request.Query.DatasetId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
            : request.Title.Trim().Replace(" ", "_");

        if (string.Equals(request.Format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = GenerateCsv(queryResult, request.Title);
            return (csvBytes, "text/csv", $"{reportTitle}.csv");
        }

        var xlsxBytes = GenerateExcel(queryResult, request.Title ?? "University Dynamic Report");
        return (xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{reportTitle}.xlsx");
    }

    public async Task<List<DynamicReportTemplateDto>> GetTemplatesAsync(ClaimsPrincipal user, string? datasetId = null, CancellationToken ct = default)
    {
        ValidateNonStudent(user);
        var currentUserId = GetUserId(user);

        var templates = new List<DynamicReportTemplateDto>();
        templates.AddRange(GetSystemPresets(datasetId));

        var query = _db.ReportCaches.AsNoTracking().Where(c => c.CacheKey != null && c.CacheKey.StartsWith("dynamic_template:"));
        var cacheEntries = await query.ToListAsync(ct);

        foreach (var entry in cacheEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.CachedData)) continue;

            try
            {
                var template = JsonSerializer.Deserialize<DynamicReportTemplateDto>(entry.CachedData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (template != null)
                {
                    if (string.IsNullOrWhiteSpace(datasetId) || string.Equals(template.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase))
                    {
                        var isOwner = entry.CacheKey?.Contains($":{currentUserId}:") == true;
                        var isShared = entry.CacheKey?.Contains(":shared:") == true;
                        if (isOwner || isShared || user.IsInRole(LmsRoles.SuperAdmin) || user.IsInRole(LmsRoles.Admin))
                        {
                            templates.Add(template);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize report template from cache key {CacheKey}", entry.CacheKey);
            }
        }

        return templates;
    }

    public async Task<DynamicReportTemplateDto> SaveTemplateAsync(ClaimsPrincipal user, SaveReportTemplateRequest request, CancellationToken ct = default)
    {
        ValidateNonStudent(user);
        var currentUserId = GetUserId(user);
        var userName = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "Staff";

        var templateId = Guid.NewGuid();
        var templateDto = new DynamicReportTemplateDto(
            templateId,
            request.Name,
            request.Description ?? string.Empty,
            request.DatasetId,
            request.SelectedFields,
            request.Filters ?? [],
            request.Sorts ?? [],
            userName,
            DateTime.UtcNow,
            false
        );

        var scope = request.IsShared ? "shared" : "private";
        var cacheKey = $"dynamic_template:{currentUserId}:{scope}:{templateId}";

        var cacheRecord = new ReportCache
        {
            Id = Guid.NewGuid(),
            ReportType = ReportType.DashboardSummary,
            CacheKey = cacheKey,
            CachedData = JsonSerializer.Serialize(templateDto),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(5)
        };

        _db.ReportCaches.Add(cacheRecord);
        await _db.SaveChangesAsync(ct);

        return templateDto;
    }

    public async Task<bool> DeleteTemplateAsync(ClaimsPrincipal user, Guid templateId, CancellationToken ct = default)
    {
        ValidateNonStudent(user);
        var currentUserId = GetUserId(user);
        var targetSuffix = $":{templateId}";

        var entry = await _db.ReportCaches.FirstOrDefaultAsync(c => c.CacheKey != null && c.CacheKey.EndsWith(targetSuffix), ct);
        if (entry == null) return false;

        var isOwner = entry.CacheKey?.Contains($":{currentUserId}:") == true;
        if (!isOwner && !user.IsInRole(LmsRoles.SuperAdmin) && !user.IsInRole(LmsRoles.Admin))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this report template.");
        }

        _db.ReportCaches.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==================== INTERNAL DATA FETCHER ====================

    private async Task<List<Dictionary<string, object?>>> FetchDatasetRowsAsync(ClaimsPrincipal user, string datasetId, CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var appUser = currentUserId.HasValue ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId.Value, ct) : null;
        var userDepartmentId = appUser?.DepartmentId;

        return datasetId.ToLowerInvariant() switch
        {
            "students" => await FetchStudentsDatasetAsync(user, currentUserId, userDepartmentId, ct),
            "course_enrollments" => await FetchCourseEnrollmentsDatasetAsync(user, currentUserId, userDepartmentId, ct),
            "financial_records" => await FetchFinancialRecordsDatasetAsync(user, ct),
            "gradebook_results" => await FetchGradebookResultsDatasetAsync(user, ct),
            "admissions_registry" => await FetchAdmissionsDatasetAsync(user, ct),
            "hostel_accommodation" => await FetchHostelDatasetAsync(user, ct),
            "timetable_attendance" => await FetchTimetableAttendanceDatasetAsync(user, currentUserId, ct),
            _ => throw new NotSupportedException($"Dataset '{datasetId}' data provider is not implemented.")
        };
    }

    private async Task<List<Dictionary<string, object?>>> FetchStudentsDatasetAsync(
        ClaimsPrincipal user,
        Guid? currentUserId,
        Guid? userDepartmentId,
        CancellationToken ct)
    {
        var query = _db.Students
            .AsNoTracking()
            .Include(s => s.AcademicProgram)
                .ThenInclude(p => p!.Department)
                    .ThenInclude(d => d.Faculty)
            .Include(s => s.Level)
            .Include(s => s.AcademicSession)
            .Include(s => s.FeeRecords)
            .Include(s => s.AdmissionApplication)
            .AsQueryable();

        if (user.IsInRole(LmsRoles.Dean) && !user.IsInRole(LmsRoles.SuperAdmin) && !user.IsInRole(LmsRoles.Admin) && !user.IsInRole(LmsRoles.ViceChancellor))
        {
            if (userDepartmentId.HasValue)
            {
                var userDept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == userDepartmentId.Value, ct);
                if (userDept?.FacultyId != null)
                {
                    query = query.Where(s => s.AcademicProgram != null && s.AcademicProgram.Department != null && s.AcademicProgram.Department.FacultyId == userDept.FacultyId);
                }
            }
        }
        else if (user.IsInRole(LmsRoles.HOD) && !user.IsInRole(LmsRoles.SuperAdmin) && !user.IsInRole(LmsRoles.Admin) && !user.IsInRole(LmsRoles.ViceChancellor))
        {
            if (userDepartmentId.HasValue)
            {
                query = query.Where(s => s.AcademicProgram != null && s.AcademicProgram.DepartmentId == userDepartmentId.Value);
            }
        }

        var students = await query.Take(1500).ToListAsync(ct);

        var studentIds = students.Select(s => s.Id).ToList();
        var standings = await _db.AcademicStandings
            .AsNoTracking()
            .Where(a => studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, ct);

        var hostels = await _db.HostelAllocations
            .AsNoTracking()
            .Include(ha => ha.HostelBed)
            .Include(ha => ha.PreferredBlock)
            .Where(ha => studentIds.Contains(ha.StudentId))
            .ToDictionaryAsync(ha => ha.StudentId, ct);

        var rows = new List<Dictionary<string, object?>>(students.Count);

        foreach (var s in students)
        {
            var prog = s.AcademicProgram;
            var dept = prog?.Department;
            var fac = dept?.Faculty;
            standings.TryGetValue(s.Id, out var standing);
            hostels.TryGetValue(s.Id, out var hostel);

            var totalBilled = s.FeeRecords?.Sum(f => f.TotalAmount) ?? 0m;
            var totalPaid = s.FeeRecords?.Sum(f => f.AmountPaid) ?? 0m;
            var balance = Math.Max(0m, totalBilled - totalPaid);
            var paymentStatus = totalBilled == 0m ? "Paid" : balance == 0m ? "Paid" : totalPaid > 0m ? "Partial" : "Unpaid";

            var app = s.AdmissionApplication;
            var jambNumber = !string.IsNullOrWhiteSpace(s.JambRegistrationNumber)
                ? s.JambRegistrationNumber
                : (!string.IsNullOrWhiteSpace(app?.JambRegNumber) ? app.JambRegNumber : string.Empty);

            var personalEmail = !string.IsNullOrWhiteSpace(s.PersonalEmail)
                ? s.PersonalEmail
                : (!string.IsNullOrWhiteSpace(app?.StudentEmail) ? app.StudentEmail : string.Empty);

            var officialEmail = !string.IsNullOrWhiteSpace(s.OfficialEmail)
                ? s.OfficialEmail
                : string.Empty;

            var parentName = !string.IsNullOrWhiteSpace(s.EmergencyContactName)
                ? s.EmergencyContactName
                : (!string.IsNullOrWhiteSpace(app?.EmergencyContactName) ? app.EmergencyContactName : string.Empty);

            var parentPhone = !string.IsNullOrWhiteSpace(s.EmergencyContactPhone)
                ? s.EmergencyContactPhone
                : (!string.IsNullOrWhiteSpace(app?.EmergencyContactPhone) ? app.EmergencyContactPhone : string.Empty);

            var parentEmail = !string.IsNullOrWhiteSpace(s.EmergencyContactEmail)
                ? s.EmergencyContactEmail
                : (!string.IsNullOrWhiteSpace(app?.EmergencyContactEmail) ? app.EmergencyContactEmail : string.Empty);

            var dob = app?.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty;
            var jambScore = s.JambScore ?? 0;

            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["student.matricNumber"] = s.StudentNumber ?? string.Empty,
                ["student.fullName"] = $"{s.FirstName} {s.LastName}".Trim(),
                ["student.firstName"] = s.FirstName,
                ["student.lastName"] = s.LastName,
                ["student.email"] = !string.IsNullOrWhiteSpace(officialEmail) ? officialEmail : personalEmail,
                ["student.officialEmail"] = officialEmail,
                ["student.personalEmail"] = personalEmail,
                ["student.gender"] = s.Gender ?? string.Empty,
                ["student.phoneNumber"] = s.Phone,
                ["student.dateOfBirth"] = dob,
                ["student.jambNumber"] = jambNumber,
                ["student.jambScore"] = jambScore,
                ["student.parentName"] = parentName,
                ["student.parentPhone"] = parentPhone,
                ["student.parentEmail"] = parentEmail,
                ["parent.guardianName"] = parentName,
                ["parent.guardianPhone"] = parentPhone,
                ["parent.guardianEmail"] = parentEmail,
                ["student.stateOfOrigin"] = string.Empty,
                ["student.nationality"] = "Nigerian",
                ["student.status"] = s.Status.ToString(),
                ["student.admissionDate"] = s.EnrollmentDate?.ToString("yyyy-MM-dd") ?? s.CreatedAt.ToString("yyyy-MM-dd"),
                ["student.studyMode"] = "Full-Time",

                ["program.programName"] = prog?.Name ?? "General Studies",
                ["program.programCode"] = prog?.Code ?? string.Empty,
                ["program.degreeType"] = prog?.DegreeAwarded ?? "BSc",

                ["department.departmentName"] = dept?.Name ?? string.Empty,
                ["department.departmentCode"] = dept?.Code ?? string.Empty,

                ["faculty.facultyName"] = fac?.Name ?? string.Empty,
                ["faculty.facultyCode"] = fac?.Label ?? "College",

                ["level.levelName"] = s.Level?.Name ?? "100 Level",

                ["standing.cumulativeGpa"] = standing?.CumulativeGpa ?? 0.00m,
                ["standing.standingType"] = standing?.StandingType.ToString() ?? "Good Standing",
                ["standing.creditsAttempted"] = standing?.TotalCreditsAttempted ?? 0,
                ["standing.creditsEarned"] = standing?.TotalCreditsEarned ?? 0,

                ["fees.totalBilled"] = totalBilled,
                ["fees.amountPaid"] = totalPaid,
                ["fees.outstandingBalance"] = balance,
                ["fees.paymentStatus"] = paymentStatus,

                ["hostel.blockName"] = hostel?.PreferredBlock?.Name ?? "Not Allocated",
                ["hostel.roomNumber"] = hostel?.PreferredRoomType ?? string.Empty,
                ["hostel.bedNumber"] = hostel?.HostelBed?.BedLabel ?? string.Empty
            };

            rows.Add(row);
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchCourseEnrollmentsDatasetAsync(
        ClaimsPrincipal user,
        Guid? currentUserId,
        Guid? userDepartmentId,
        CancellationToken ct)
    {
        var query = _db.CourseEnrollments
            .AsNoTracking()
            .Include(ce => ce.Student)
            .Include(ce => ce.CourseOffering)
                .ThenInclude(co => co.Course)
                    .ThenInclude(c => c.Program)
                        .ThenInclude(p => p.Department)
            .Include(ce => ce.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Include(ce => ce.CourseOffering)
                .ThenInclude(co => co.Lecturers)
                    .ThenInclude(col => col.Lecturer)
            .AsQueryable();

        // Row-Level Security
        if (user.IsInRole(LmsRoles.Lecturer) && !user.IsInRole(LmsRoles.SuperAdmin) && !user.IsInRole(LmsRoles.Admin) && !user.IsInRole(LmsRoles.ViceChancellor) && !user.IsInRole(LmsRoles.Dean) && !user.IsInRole(LmsRoles.HOD))
        {
            if (currentUserId.HasValue)
            {
                query = query.Where(ce => ce.CourseOffering.Lecturers.Any(l => l.LecturerId == currentUserId.Value));
            }
        }
        else if (user.IsInRole(LmsRoles.HOD) && !user.IsInRole(LmsRoles.SuperAdmin) && !user.IsInRole(LmsRoles.Admin) && !user.IsInRole(LmsRoles.ViceChancellor))
        {
            if (userDepartmentId.HasValue)
            {
                query = query.Where(ce => ce.CourseOffering.Course.Program.DepartmentId == userDepartmentId.Value);
            }
        }

        var enrollments = await query.Take(1500).ToListAsync(ct);

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var offeringIds = enrollments.Select(e => e.CourseOfferingId).Distinct().ToList();

        var results = await _db.StudentCourseResults
            .AsNoTracking()
            .Where(r => studentIds.Contains(r.StudentId) && offeringIds.Contains(r.CourseOfferingId))
            .ToDictionaryAsync(r => $"{r.StudentId}_{r.CourseOfferingId}", ct);

        var attendances = await _db.SessionAttendances
            .AsNoTracking()
            .Where(a => studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Present = g.Count(a => a.IsPresent),
                Total = g.Count()
            })
            .ToDictionaryAsync(a => a.StudentId, ct);

        var rows = new List<Dictionary<string, object?>>(enrollments.Count);

        foreach (var ce in enrollments)
        {
            var s = ce.Student;
            var co = ce.CourseOffering;
            var c = co?.Course;
            var lecturer = co?.Lecturers?.FirstOrDefault()?.Lecturer;
            results.TryGetValue($"{ce.StudentId}_{ce.CourseOfferingId}", out var res);
            attendances.TryGetValue(ce.StudentId, out var att);

            var totalSessions = att?.Total ?? 0;
            var attended = att?.Present ?? 0;
            var attPercent = totalSessions > 0 ? Math.Round((decimal)attended / totalSessions * 100, 1) : 100m;

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["student.matricNumber"] = s?.Username ?? string.Empty,
                ["student.fullName"] = s?.DisplayName ?? s?.Username ?? string.Empty,
                ["student.email"] = s?.Email ?? string.Empty,
                ["student.program"] = c?.Program?.Name ?? "Enrolled Program",
                ["student.level"] = "Undergraduate",

                ["course.courseCode"] = c?.Code ?? string.Empty,
                ["course.courseTitle"] = c?.Title ?? string.Empty,
                ["course.creditUnits"] = c?.CreditUnits ?? 0,

                ["courseOffering.sessionName"] = co?.AcademicSession?.Name ?? string.Empty,
                ["courseOffering.semester"] = co?.Semester == Semester.First ? "First Semester" : "Second Semester",
                ["courseOffering.section"] = "Main",

                ["lecturer.lecturerName"] = lecturer?.DisplayName ?? lecturer?.Username ?? "TBA",
                ["lecturer.lecturerEmail"] = lecturer?.Email ?? string.Empty,

                ["grades.continuousAssessment"] = (res?.Ca1Score ?? 0m) + (res?.Ca2Score ?? 0m) + (res?.Ca3Score ?? 0m),
                ["grades.examScore"] = res?.ExamScore ?? 0m,
                ["grades.totalScore"] = res?.TotalScore ?? 0m,
                ["grades.gradeLetter"] = !string.IsNullOrWhiteSpace(res?.LetterGrade) ? res.LetterGrade : "N/A",
                ["grades.gradePoint"] = res?.GradePoints ?? 0m,

                ["attendance.sessionsAttended"] = attended,
                ["attendance.totalSessions"] = totalSessions,
                ["attendance.attendancePercentage"] = attPercent
            });
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchFinancialRecordsDatasetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var payments = await _db.FeePayments
            .AsNoTracking()
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Student)
                    .ThenInclude(s => s.AcademicProgram)
                        .ThenInclude(prog => prog!.Department)
                            .ThenInclude(d => d.Faculty)
            .OrderByDescending(p => p.PaidAt)
            .Take(1500)
            .ToListAsync(ct);

        var rows = new List<Dictionary<string, object?>>(payments.Count);

        foreach (var p in payments)
        {
            var s = p.StudentFeeRecord?.Student;
            var prog = s?.AcademicProgram;
            var fac = prog?.Department?.Faculty;

            var methodStr = p.PaymentMethod switch
            {
                PaymentMethod.Paystack => "Paystack",
                PaymentMethod.Hydrogen => "Hydrogen",
                _ => "Manual / Bank"
            };

            var refNumber = !string.IsNullOrWhiteSpace(p.GatewayReference)
                ? p.GatewayReference
                : !string.IsNullOrWhiteSpace(p.ReferenceNumber)
                    ? p.ReferenceNumber
                    : p.Id.ToString()[..8];

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["payment.reference"] = refNumber,
                ["payment.amount"] = p.Amount,
                ["payment.paymentDate"] = p.PaidAt.ToString("yyyy-MM-dd"),
                ["payment.paymentMethod"] = methodStr,
                ["payment.status"] = p.Status.ToString(),
                ["payment.receiptNumber"] = refNumber,

                ["student.matricNumber"] = s?.StudentNumber ?? string.Empty,
                ["student.fullName"] = $"{s?.FirstName} {s?.LastName}".Trim(),
                ["student.programName"] = prog?.Name ?? "General Studies",
                ["student.levelName"] = "Undergraduate",
                ["student.facultyName"] = fac?.Name ?? string.Empty,

                ["feeTemplate.templateName"] = "Academic Session Fees",
                ["feeTemplate.categoryName"] = "Tuition & Sundry",

                ["scholarship.scholarshipName"] = "None",
                ["scholarship.sponsorName"] = "Self-Sponsored",
                ["scholarship.discountAmount"] = 0m
            });
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchGradebookResultsDatasetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var results = await _db.StudentCourseResults
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(r => r.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(1500)
            .ToListAsync(ct);

        var rows = new List<Dictionary<string, object?>>(results.Count);

        foreach (var r in results)
        {
            var s = r.Student;
            var co = r.CourseOffering;
            var c = co?.Course;

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["result.totalScore"] = r.TotalScore,
                ["result.gradeLetter"] = !string.IsNullOrWhiteSpace(r.LetterGrade) ? r.LetterGrade : "F",
                ["result.gradePoint"] = r.GradePoints,
                ["result.isPassed"] = r.TotalScore >= 40m ? "Passed" : "Failed",
                ["result.publishedStatus"] = r.IsPublished ? "Senate Published" : "Dean Approved",

                ["student.matricNumber"] = s?.Username ?? string.Empty,
                ["student.fullName"] = s?.DisplayName ?? s?.Username ?? string.Empty,
                ["student.levelName"] = "Undergraduate",
                ["student.programName"] = "Academic Program",

                ["course.courseCode"] = c?.Code ?? string.Empty,
                ["course.courseTitle"] = c?.Title ?? string.Empty,

                ["session.sessionName"] = co?.AcademicSession?.Name ?? string.Empty,
                ["session.semester"] = co?.Semester == Semester.First ? "1st Semester" : "2nd Semester",

                ["lecturer.lecturerName"] = "Course Instructor"
            });
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchAdmissionsDatasetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var applications = await _db.AdmissionApplications
            .AsNoTracking()
            .Include(a => a.AcademicProgram)
                .ThenInclude(p => p!.Department)
                    .ThenInclude(d => d.Faculty)
            .OrderByDescending(a => a.CreatedAt)
            .Take(1500)
            .ToListAsync(ct);

        var rows = new List<Dictionary<string, object?>>(applications.Count);

        foreach (var a in applications)
        {
            var prog = a.AcademicProgram;
            var fac = prog?.Department?.Faculty;

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["application.applicationNumber"] = !string.IsNullOrWhiteSpace(a.ApplicationNumber) ? a.ApplicationNumber : a.Id.ToString()[..8],
                ["application.applicantName"] = $"{a.FirstName} {a.LastName}".Trim(),
                ["application.email"] = a.StudentEmail,
                ["application.phoneNumber"] = a.Phone,
                ["application.gender"] = "Unspecified",
                ["application.stateOfOrigin"] = a.CountryOfOrigin ?? string.Empty,
                ["application.jambRegNo"] = a.JambRegNumber,
                ["application.jambScore"] = 200,
                ["application.dateOfBirth"] = a.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["application.parentName"] = a.EmergencyContactName ?? string.Empty,
                ["application.parentPhone"] = a.EmergencyContactPhone ?? string.Empty,
                ["application.parentEmail"] = a.EmergencyContactEmail ?? string.Empty,
                ["application.status"] = a.Status.ToString(),
                ["application.submissionDate"] = a.SubmittedAt?.ToString("yyyy-MM-dd") ?? a.CreatedAt.ToString("yyyy-MM-dd"),

                ["program.firstChoiceProgram"] = prog?.Name ?? "General Studies",
                ["program.admittedProgram"] = a.Status == AdmissionStatus.Admitted || a.Status == AdmissionStatus.OfferAccepted ? prog?.Name ?? string.Empty : "Pending",
                ["program.departmentName"] = prog?.Department?.Name ?? string.Empty,
                ["program.facultyName"] = fac?.Name ?? string.Empty,

                ["decision.offerAccepted"] = a.OfferAcceptedAt.HasValue ? "Accepted" : "Pending",
                ["decision.acceptanceFeePaid"] = a.Status == AdmissionStatus.FeePaid ? "Paid" : "Unpaid",
                ["decision.matricNumberAssigned"] = a.StudentId.HasValue ? "Assigned" : "Unassigned"
            });
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchHostelDatasetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var allocations = await _db.HostelAllocations
            .AsNoTracking()
            .Include(ha => ha.Student)
                .ThenInclude(s => s!.AcademicProgram)
            .Include(ha => ha.HostelBed)
                .ThenInclude(b => b!.HostelRoom)
                    .ThenInclude(r => r!.HostelBlock)
            .OrderByDescending(ha => ha.AllocatedAt)
            .Take(1500)
            .ToListAsync(ct);

        var rows = new List<Dictionary<string, object?>>(allocations.Count);

        foreach (var ha in allocations)
        {
            var s = ha.Student;
            var bed = ha.HostelBed;
            var room = bed?.HostelRoom;
            var block = room?.HostelBlock;

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["allocation.allocationDate"] = ha.AllocatedAt?.ToString("yyyy-MM-dd") ?? ha.ApplicationDate.ToString("yyyy-MM-dd"),
                ["allocation.status"] = ha.Status.ToString(),
                ["allocation.sessionName"] = "Current Session",

                ["hostel.blockName"] = block?.Name ?? "Hall of Residence",
                ["hostel.genderType"] = block?.GenderType.ToString() ?? "Coed",
                ["hostel.roomNumber"] = room?.RoomNumber ?? string.Empty,
                ["hostel.bedNumber"] = bed?.BedLabel ?? string.Empty,
                ["hostel.roomCapacity"] = room?.Capacity ?? 4,

                ["student.matricNumber"] = s?.StudentNumber ?? string.Empty,
                ["student.fullName"] = $"{s?.FirstName} {s?.LastName}".Trim(),
                ["student.gender"] = s?.Gender ?? string.Empty,
                ["student.levelName"] = s?.Level?.Name ?? "100 Level",
                ["student.programName"] = s?.AcademicProgram?.Name ?? string.Empty,
                ["student.phoneNumber"] = s?.Phone ?? string.Empty,

                ["exeat.totalExeats"] = 0,
                ["exeat.activeExeatStatus"] = "On Campus"
            });
        }

        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> FetchTimetableAttendanceDatasetAsync(
        ClaimsPrincipal user,
        Guid? currentUserId,
        CancellationToken ct)
    {
        var sessions = await _db.LectureSessions
            .AsNoTracking()
            .Include(ls => ls.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(ls => ls.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(ls => ls.Attendance)
            .OrderByDescending(ls => ls.SessionDate)
            .Take(1500)
            .ToListAsync(ct);

        var rows = new List<Dictionary<string, object?>>(sessions.Count);

        foreach (var s in sessions)
        {
            var co = s.CourseOffering;
            var c = co?.Course;
            var lecturer = s.SessionLecturers?.FirstOrDefault()?.Lecturer;
            var attendances = s.Attendance ?? [];
            var presentCount = attendances.Count(a => a.IsPresent);
            var totalCount = Math.Max(attendances.Count, 1);
            var rate = attendances.Count > 0 ? Math.Round((decimal)presentCount / attendances.Count * 100, 1) : 100m;

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["session.sessionDate"] = s.SessionDate.ToString("yyyy-MM-dd"),
                ["session.startTime"] = s.StartTime.ToString(@"hh\:mm"),
                ["session.endTime"] = s.EndTime.ToString(@"hh\:mm"),
                ["session.venue"] = "Main Lecture Theater",
                ["session.sessionType"] = s.IsManuallyCreated ? "Special Session" : "Scheduled Lecture",
                ["session.status"] = s.IsCompleted ? "Completed" : "Scheduled",

                ["course.courseCode"] = c?.Code ?? string.Empty,
                ["course.courseTitle"] = c?.Title ?? string.Empty,

                ["lecturer.lecturerName"] = lecturer?.DisplayName ?? lecturer?.Username ?? "Faculty Staff",
                ["lecturer.lecturerEmail"] = lecturer?.Email ?? string.Empty,

                ["attendanceSummary.enrolledCount"] = totalCount,
                ["attendanceSummary.presentCount"] = presentCount,
                ["attendanceSummary.absentCount"] = Math.Max(0, totalCount - presentCount),
                ["attendanceSummary.attendanceRate"] = rate
            });
        }

        return rows;
    }

    // ==================== FILTERING & SORTING ====================

    private static List<Dictionary<string, object?>> ApplyFilters(
        List<Dictionary<string, object?>> rows,
        List<ReportFilterRuleDto>? filters,
        string? searchTerm,
        Dictionary<string, ReportFieldMetadataDto> fieldMeta)
    {
        var result = rows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            result = result.Where(row =>
                row.Values.Any(val => val != null && val.ToString()!.Contains(term, StringComparison.OrdinalIgnoreCase))
            );
        }

        if (filters == null || filters.Count == 0)
        {
            return result.ToList();
        }

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.FieldId) || string.IsNullOrWhiteSpace(filter.Operator))
                continue;

            result = result.Where(row => MatchesFilter(row, filter, fieldMeta));
        }

        return result.ToList();
    }

    private static bool MatchesFilter(
        Dictionary<string, object?> row,
        ReportFilterRuleDto filter,
        Dictionary<string, ReportFieldMetadataDto> fieldMeta)
    {
        row.TryGetValue(filter.FieldId, out var rawVal);
        fieldMeta.TryGetValue(filter.FieldId, out var meta);

        var isNullOp = filter.Operator.Equals("isNull", StringComparison.OrdinalIgnoreCase);
        var isNotNullOp = filter.Operator.Equals("isNotNull", StringComparison.OrdinalIgnoreCase);

        if (isNullOp) return rawVal == null || string.IsNullOrWhiteSpace(rawVal.ToString());
        if (isNotNullOp) return rawVal != null && !string.IsNullOrWhiteSpace(rawVal.ToString());

        if (rawVal == null) return false;

        var valStr = rawVal.ToString()!;
        var targetStr = filter.Value ?? string.Empty;

        if (meta?.DataType is "number" or "currency")
        {
            var numVal = ConvertToDecimal(rawVal);
            var targetNum = decimal.TryParse(filter.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 0m;
            var targetNumTo = decimal.TryParse(filter.ValueTo, NumberStyles.Any, CultureInfo.InvariantCulture, out var tTo) ? tTo : 0m;

            return filter.Operator.ToLowerInvariant() switch
            {
                "equals" => numVal == targetNum,
                "notequals" => numVal != targetNum,
                "greaterthan" => numVal > targetNum,
                "lessthan" => numVal < targetNum,
                "greaterthanorequal" => numVal >= targetNum,
                "lessthanorequal" => numVal <= targetNum,
                "between" => numVal >= targetNum && numVal <= targetNumTo,
                _ => true
            };
        }

        if (meta?.DataType is "date")
        {
            if (DateTime.TryParse(valStr, out var dVal) && DateTime.TryParse(targetStr, out var dTarget))
            {
                return filter.Operator.ToLowerInvariant() switch
                {
                    "equals" => dVal.Date == dTarget.Date,
                    "greaterthan" => dVal.Date > dTarget.Date,
                    "lessthan" => dVal.Date < dTarget.Date,
                    "between" => DateTime.TryParse(filter.ValueTo, out var dTargetTo) && dVal.Date >= dTarget.Date && dVal.Date <= dTargetTo.Date,
                    _ => true
                };
            }
        }

        return filter.Operator.ToLowerInvariant() switch
        {
            "equals" => string.Equals(valStr, targetStr, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(valStr, targetStr, StringComparison.OrdinalIgnoreCase),
            "contains" => valStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase),
            "startswith" => valStr.StartsWith(targetStr, StringComparison.OrdinalIgnoreCase),
            "in" => targetStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .Any(v => string.Equals(v, valStr, StringComparison.OrdinalIgnoreCase)),
            _ => true
        };
    }

    private static List<Dictionary<string, object?>> ApplySorting(
        List<Dictionary<string, object?>> rows,
        List<ReportSortRuleDto>? sorts,
        Dictionary<string, ReportFieldMetadataDto> fieldMeta)
    {
        if (sorts == null || sorts.Count == 0) return rows;

        IOrderedEnumerable<Dictionary<string, object?>>? ordered = null;

        for (int i = 0; i < sorts.Count; i++)
        {
            var sort = sorts[i];
            fieldMeta.TryGetValue(sort.FieldId, out var meta);

            object KeySelector(Dictionary<string, object?> r)
            {
                r.TryGetValue(sort.FieldId, out var v);
                if (meta?.DataType is "number" or "currency")
                {
                    return v != null ? ConvertToDecimal(v) : 0m;
                }
                if (meta?.DataType is "date")
                {
                    if (v != null && DateTime.TryParse(v.ToString(), out var dt))
                        return dt;
                    return sort.Descending ? DateTime.MinValue : DateTime.MaxValue;
                }
                return v?.ToString() ?? string.Empty;
            }

            if (i == 0)
            {
                ordered = sort.Descending ? rows.OrderByDescending(KeySelector) : rows.OrderBy(KeySelector);
            }
            else
            {
                ordered = sort.Descending ? ordered!.ThenByDescending(KeySelector) : ordered!.ThenBy(KeySelector);
            }
        }

        return ordered?.ToList() ?? rows;
    }

    private static decimal ConvertToDecimal(object val)
    {
        if (val is decimal d) return d;
        if (val is double dbl) return (decimal)dbl;
        if (val is int i) return i;
        if (val is long l) return l;
        if (decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return 0m;
    }

    // ==================== EXCEL & CSV GENERATORS ====================

    private static byte[] GenerateExcel(DynamicReportResultDto result, string title)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Dynamic Report");

        worksheet.Cell(1, 1).Value = "WIGWE UNIVERSITY - ENTERPRISE ACADEMIC PORTAL";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#004B44");

        worksheet.Cell(2, 1).Value = $"{title} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Records: {result.TotalCount:N0}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 10;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        int headerRowIndex = 4;
        for (int c = 0; c < result.Columns.Count; c++)
        {
            var col = result.Columns[c];
            var cell = worksheet.Cell(headerRowIndex, c + 1);
            cell.Value = col.Label;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#004B44");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#003530");
        }

        int currentRow = 5;
        foreach (var row in result.Rows)
        {
            bool isEven = (currentRow % 2) == 0;
            var rowBg = isEven ? XLColor.FromHtml("#F8FAFC") : XLColor.White;

            for (int c = 0; c < result.Columns.Count; c++)
            {
                var col = result.Columns[c];
                var cell = worksheet.Cell(currentRow, c + 1);
                row.TryGetValue(col.FieldId, out var val);

                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

                if (val == null || string.IsNullOrWhiteSpace(val.ToString()))
                {
                    cell.Value = "-";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    continue;
                }

                if (col.DataType is "currency")
                {
                    cell.Value = ConvertToDecimal(val);
                    cell.Style.NumberFormat.Format = "₦#,##0.00";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                else if (col.DataType is "number")
                {
                    cell.Value = ConvertToDecimal(val);
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                else if (col.DataType is "date")
                {
                    cell.Value = val.ToString();
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    cell.Value = val.ToString();
                }
            }

            currentRow++;
        }

        worksheet.Columns().AdjustToContents(1, 50);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateCsv(DynamicReportResultDto result, string? title)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(",", result.Columns.Select(c => EscapeCsv(c.Label))));

        foreach (var row in result.Rows)
        {
            var line = result.Columns.Select(col =>
            {
                row.TryGetValue(col.FieldId, out var val);
                return EscapeCsv(val?.ToString() ?? string.Empty);
            });
            sb.AppendLine(string.Join(",", line));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    // ==================== SYSTEM PRESETS ====================

    private static List<DynamicReportTemplateDto> GetSystemPresets(string? datasetId)
    {
        var presets = new List<DynamicReportTemplateDto>
        {
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111101"),
                "Students in Financial Arrears (Debtors)",
                "All students with outstanding balance greater than ₦0.00 across programs and faculties.",
                "students",
                ["student.matricNumber", "student.fullName", "program.programName", "level.levelName", "fees.totalBilled", "fees.amountPaid", "fees.outstandingBalance", "fees.paymentStatus"],
                [new ReportFilterRuleDto("fees.outstandingBalance", "greaterThan", "0")],
                [new ReportSortRuleDto("fees.outstandingBalance", true)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111102"),
                "Dean's List / High Academic Achievers",
                "Students maintaining a Cumulative GPA of 3.50 or higher.",
                "students",
                ["student.matricNumber", "student.fullName", "program.programName", "level.levelName", "standing.cumulativeGpa", "standing.standingType", "standing.creditsEarned"],
                [new ReportFilterRuleDto("standing.cumulativeGpa", "greaterThanOrEqual", "3.50")],
                [new ReportSortRuleDto("standing.cumulativeGpa", true)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111103"),
                "Students on Academic Probation",
                "Students with Cumulative GPA below 1.50 at risk of academic probation or suspension.",
                "students",
                ["student.matricNumber", "student.fullName", "program.programName", "level.levelName", "standing.cumulativeGpa", "standing.standingType"],
                [new ReportFilterRuleDto("standing.cumulativeGpa", "lessThan", "1.50")],
                [new ReportSortRuleDto("standing.cumulativeGpa", false)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111104"),
                "Course Attendance Deficit (< 75%)",
                "Students whose attendance in assigned courses has dropped below the 75% exam qualification threshold.",
                "course_enrollments",
                ["student.matricNumber", "student.fullName", "course.courseCode", "course.courseTitle", "lecturer.lecturerName", "attendance.attendancePercentage"],
                [new ReportFilterRuleDto("attendance.attendancePercentage", "lessThan", "75")],
                [new ReportSortRuleDto("attendance.attendancePercentage", false)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111105"),
                "Verified Admission Acceptance Cohort",
                "Admitted applicants who have accepted their offer and paid their acceptance fee.",
                "admissions_registry",
                ["application.applicationNumber", "application.applicantName", "program.admittedProgram", "decision.offerAccepted", "decision.acceptanceFeePaid", "decision.matricNumberAssigned"],
                [
                    new ReportFilterRuleDto("decision.offerAccepted", "equals", "Accepted"),
                    new ReportFilterRuleDto("decision.acceptanceFeePaid", "equals", "Paid")
                ],
                [new ReportSortRuleDto("application.applicantName", false)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111106"),
                "Active Hostel Bed Allocations",
                "Current session hostel residents with room numbers and bed space allocations.",
                "hostel_accommodation",
                ["hostel.blockName", "hostel.roomNumber", "hostel.bedNumber", "student.matricNumber", "student.fullName", "student.gender", "allocation.status"],
                [new ReportFilterRuleDto("allocation.status", "equals", "Allocated")],
                [new ReportSortRuleDto("hostel.blockName", false)],
                "System Preset",
                DateTime.UtcNow,
                true
            ),
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111107"),
                "Students with Incomplete Biodata / Missing Records",
                "Audit report identifying students with missing JAMB numbers, unsupplied personal emails, missing parent/guardian contacts, or date of birth.",
                "students",
                ["student.matricNumber", "student.fullName", "program.programName", "student.jambNumber", "student.personalEmail", "student.parentName", "student.parentPhone", "student.dateOfBirth"],
                [new ReportFilterRuleDto("student.jambNumber", "isNull", null)],
                [new ReportSortRuleDto("student.fullName", false)],
                "System Preset",
                DateTime.UtcNow,
                true
            )
        };

        if (!string.IsNullOrWhiteSpace(datasetId))
        {
            return presets.Where(p => string.Equals(p.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return presets;
    }

    private static void ValidateNonStudent(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Authentication is required to access the dynamic report builder.");
        }

        if (user.IsInRole(LmsRoles.Student))
        {
            throw new UnauthorizedAccessException("Students are strictly forbidden from accessing the dynamic report builder.");
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid")
            ?? user.FindFirstValue("userId");

        return Guid.TryParse(idClaim, out var id) ? id : null;
    }
}
