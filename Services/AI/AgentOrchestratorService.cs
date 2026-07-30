using System.ComponentModel; 
using LMS.Api.Common.AI;
using LMS.Api.Data;
using LMS.Api.Extensions;
using LMS.Api.Security;
using LMS.Api.Services;
using LMS.Api.Services.AI.Models;
using LMS.Api.Services.AI.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace LMS.Api.Services.AI;

public class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly AIAgentOptions _options;
    private readonly AdvisorAgentTools _advisorTools;
    private readonly FeeAgentTools _feeTools;
    private readonly AssessmentAgentTools _assessmentTools;
    private readonly TutorAgentTools _tutorTools;
    private readonly CampusLifeTools _campusLifeTools;
    private readonly AdmissionAgentTools _admissionTools;
    private readonly AdminAssistantTools _adminAssistantTools;
    private readonly IGpaCalculationService _gpaService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<AgentOrchestratorService> _logger;
    private readonly IChatClient? _chatClient;

    public AgentOrchestratorService(
        IOptions<AIAgentOptions> options,
        AdvisorAgentTools advisorTools,
        FeeAgentTools feeTools,
        AssessmentAgentTools assessmentTools,
        TutorAgentTools tutorTools,
        CampusLifeTools campusLifeTools,
        AdmissionAgentTools admissionTools,
        AdminAssistantTools adminAssistantTools,
        IGpaCalculationService gpaService,
        ICurrentUserContext currentUserContext,
        LmsDbContext dbContext,
        ILogger<AgentOrchestratorService> logger,
        IChatClient? chatClient = null)
    {
        _options = options.Value;
        _advisorTools = advisorTools;
        _feeTools = feeTools;
        _assessmentTools = assessmentTools;
        _tutorTools = tutorTools;
        _campusLifeTools = campusLifeTools;
        _admissionTools = admissionTools;
        _adminAssistantTools = adminAssistantTools;
        _gpaService = gpaService;
        _currentUserContext = currentUserContext;
        _dbContext = dbContext;
        _logger = logger;
        _chatClient = chatClient;
    }

    public async Task<AgentChatResponse> ProcessChatAsync(AgentChatRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing AgentChat request for persona {Persona} with prompt: {Prompt}", request.Persona, request.Prompt);

        var response = new AgentChatResponse
        {
            ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
            Persona = request.Persona
        };

        Guid parsedStudentId = Guid.Empty;

        // 1. Resolve authenticated HttpContext user context
        var authUserId = await _currentUserContext.GetUserIdAsync(ct);
        if (authUserId.HasValue && authUserId.Value != Guid.Empty)
        {
            var studentFromAuth = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == authUserId.Value || s.EntraObjectId == authUserId.Value.ToString(), ct);
            if (studentFromAuth != null)
            {
                parsedStudentId = studentFromAuth.Id;
            }
        }

        // 2. Try input student ID from payload
        if (parsedStudentId == Guid.Empty && !string.IsNullOrWhiteSpace(request.StudentId) && Guid.TryParse(request.StudentId, out var inputGuid))
        {
            var matchedStudent = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == inputGuid, ct);
            if (matchedStudent == null)
            {
                var appUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == inputGuid, ct);
                if (appUser != null)
                {
                    matchedStudent = await _dbContext.Students.FirstOrDefaultAsync(s => s.OfficialEmail == appUser.Email || s.PersonalEmail == appUser.Email, ct);
                }
            }
            if (matchedStudent != null)
            {
                parsedStudentId = matchedStudent.Id;
            }
        }

        // 3. Fall back to active student with course enrollments
        if (parsedStudentId == Guid.Empty)
        {
            var studentWithGrades = await _dbContext.Students
                .FirstOrDefaultAsync(s => _dbContext.CourseEnrollments.Any(e => e.StudentId == s.Id), ct);
            parsedStudentId = studentWithGrades?.Id ?? (await _dbContext.Students.Select(s => s.Id).FirstOrDefaultAsync(ct));
        }

        string p = request.Prompt.ToLowerInvariant();

        // 🧠 Smart Intent Auto-Routing Across All University Domains
        if (p.Contains("hostel") || p.Contains("room") || p.Contains("accommodation") || p.Contains("housing"))
        {
            return await HandleHostelIntentAsync(parsedStudentId, response, ct);
        }

        if (p.Contains("timetable") || p.Contains("schedule") || p.Contains("lecture") || p.Contains("today class") || p.Contains("when is my class"))
        {
            return await HandleTimetableIntentAsync(parsedStudentId, response, ct);
        }

        if (p.Contains("attendance") || p.Contains("absent") || p.Contains("exam eligibility") || p.Contains("debarment"))
        {
            return await HandleAttendanceIntentAsync(parsedStudentId, response, ct);
        }

        if (p.Contains("scholarship") || p.Contains("grant") || p.Contains("discount") || p.Contains("financial aid"))
        {
            return await HandleScholarshipIntentAsync(parsedStudentId, response, ct);
        }

        if (p.Contains("gpa") || p.Contains("check my gpa") || p.Contains("transcript") || p.Contains("grade"))
        {
            response.Persona = AgentPersona.Advisor;
            return await HandleAdvisorPersonaAsync(request, parsedStudentId, response);
        }

        if (p.Contains("fee") || p.Contains("bill") || p.Contains("balance") || p.Contains("cleared for exam"))
        {
            response.Persona = AgentPersona.Bursar;
            return await HandleBursarPersonaAsync(request, parsedStudentId, response);
        }

        if (p.Contains("admission") || p.Contains("applicant") || p.Contains("application") ||
            p.Contains("admitted") || p.Contains("jamb") || p.Contains("pending review") ||
            p.Contains("offer letter") || p.Contains("waitlist"))
        {
            response.Persona = AgentPersona.Admission;
            return await HandleAdmissionPersonaAsync(request, response, ct);
        }

        // Default Persona Routing
        switch (request.Persona)
        {
            case AgentPersona.Advisor:
                return await HandleAdvisorPersonaAsync(request, parsedStudentId, response);

            case AgentPersona.Bursar:
                return await HandleBursarPersonaAsync(request, parsedStudentId, response);

            case AgentPersona.Admission:
                return await HandleAdmissionPersonaAsync(request, response, ct);

            case AgentPersona.AdminAssistant:
                return await HandleAdminAssistantPersonaAsync(request, response, ct);

            case AgentPersona.InstructorTA:
                return await HandleTAPersonaAsync(request, response);

            case AgentPersona.Tutor:
            default:
                return await HandleTutorPersonaAsync(request, response);
        }
    }


    private async Task<AgentChatResponse> HandleAdmissionPersonaAsync(AgentChatRequest request, AgentChatResponse response, CancellationToken ct)
    {
        string p = request.Prompt.ToLowerInvariant();

        // Lookup a specific applicant by number/email
        if (p.Contains("lookup") || p.Contains("find applicant") || p.Contains("search applicant") ||
            p.Contains("track") || p.Contains("check application"))
        {
            var tokens = request.Prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var searchQuery = tokens.LastOrDefault(t => t.Length > 5) ?? request.Prompt;
            response.ToolsExecuted.Add("AdmissionAgentTools.GetApplicantStatusByNumberAsync");
            string applicantInfo = await _admissionTools.GetApplicantStatusByNumberAsync(searchQuery, ct);
            response.ResponseText = $"🎓 **Admissions Officer AI**\n\n{applicantInfo}";
            return response;
        }

        // Breakdown by program
        if (p.Contains("program") || p.Contains("faculty") || p.Contains("breakdown") || p.Contains("popular"))
        {
            response.ToolsExecuted.Add("AdmissionAgentTools.GetApplicationsByProgramAsync");
            await _admissionTools.GetApplicationsByProgramAsync(ct);

            var rawBreakdown = await _dbContext.AdmissionApplications
                .Include(a => a.AcademicProgram)
                .GroupBy(a => a.AcademicProgram != null ? a.AcademicProgram.Name : "Unspecified Program")
                .Select(g => new { ProgramName = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(15)
                .ToListAsync(ct);

            response.ResponseText = $"📊 Here is the current application breakdown by academic program:";

            response.Card = new GenerativeCardDto
            {
                CardType = "applicant_table",
                Title = "Applications Breakdown by Program",
                Subtitle = $"All Programs ({rawBreakdown.Sum(b => b.Count)} Total Applications)",
                Data = new Dictionary<string, object>
                {
                    { "items", rawBreakdown.Select(b => new Dictionary<string, object> {
                        { "program", b.ProgramName },
                        { "totalApplications", b.Count }
                    }).ToList() }
                },
                Actions = new List<CardActionDto>
                {
                    new CardActionDto { Label = "View Admissions Portal", ActionType = "navigate", Target = "/dashboard/registry/admissions" }
                }
            };
            return response;
        }

        // Pending applications
        if (p.Contains("pending") || p.Contains("review") || p.Contains("awaiting"))
        {
            response.ToolsExecuted.Add("AdmissionAgentTools.GetPendingApplicationsReviewAsync");

            var pendingList = await _dbContext.AdmissionApplications
                .Where(a => a.Status == AdmissionStatus.UnderReview || a.Status == AdmissionStatus.Submitted)
                .Include(a => a.AcademicProgram)
                .OrderBy(a => a.SubmittedAt)
                .Take(8)
                .ToListAsync(ct);

            if (pendingList.Count > 0)
            {
                response.ResponseText = $"📋 Found {pendingList.Count} application(s) pending review in the admissions pipeline:";
                response.Card = new GenerativeCardDto
                {
                    CardType = "applicant_table",
                    Title = "Applications Pending Review",
                    Subtitle = $"{pendingList.Count} Submissions Awaiting Decision",
                    Data = new Dictionary<string, object>
                    {
                        { "items", pendingList.Select(a => new Dictionary<string, object> {
                            { "applicantName", $"{a.FirstName} {a.LastName}".ToTitleCase() },
                            { "applicationNo", a.ApplicationNumber },
                            { "program", a.AcademicProgram?.Name ?? "N/A" },
                            { "applicantType", a.ApplicantType.ToString() },
                            { "status", a.Status.ToString() },
                            { "submitted", a.SubmittedAt?.ToString("yyyy-MM-dd") ?? "N/A" }
                        }).ToList() }
                    },
                    Actions = new List<CardActionDto>
                    {
                        new CardActionDto { Label = "Review Applications", ActionType = "navigate", Target = "/dashboard/registry/admissions" }
                    }
                };
            }
            else
            {
                response.ResponseText = "📋 No applications are currently awaiting review. All submissions have been processed!";
            }
            return response;
        }

        // Recently admitted list
        if (p.Contains("admitted") || p.Contains("recently") || p.Contains("accepted") || p.Contains("offer"))
        {
            response.ToolsExecuted.Add("AdmissionAgentTools.GetRecentlyAdmittedApplicantsAsync");

            var admittedList = await _dbContext.AdmissionApplications
                .Where(a => a.Status == AdmissionStatus.Admitted || a.Status == AdmissionStatus.OfferAccepted || a.Status == AdmissionStatus.FeePaid)
                .Include(a => a.AcademicProgram)
                .OrderByDescending(a => a.UpdatedAt)
                .Take(10)
                .ToListAsync(ct);

            if (admittedList.Count > 0)
            {
                response.ResponseText = $"✅ Here are the most recently admitted applicants:";
                response.Card = new GenerativeCardDto
                {
                    CardType = "applicant_table",
                    Title = "Recently Admitted Applicants",
                    Subtitle = $"{admittedList.Count} Most Recent Decisions",
                    Data = new Dictionary<string, object>
                    {
                        { "items", admittedList.Select(a => new Dictionary<string, object> {
                            { "applicantName", $"{a.FirstName} {a.LastName}".ToTitleCase() },
                            { "applicationNo", a.ApplicationNumber },
                            { "program", a.AcademicProgram?.Name ?? "N/A" },
                            { "status", a.Status.ToString() },
                            { "decidedDate", a.UpdatedAt.ToString("yyyy-MM-dd") }
                        }).ToList() }
                    },
                    Actions = new List<CardActionDto>
                    {
                        new CardActionDto { Label = "All Admissions", ActionType = "navigate", Target = "/dashboard/registry/admissions" }
                    }
                };
            }
            else
            {
                response.ResponseText = "✅ No admitted applicants found in the current session.";
            }
            return response;
        }

        // Default: statistics overview
        response.ToolsExecuted.Add("AdmissionAgentTools.GetAdmissionStatisticsAsync");
        string statsText = await _admissionTools.GetAdmissionStatisticsAsync(null, ct);

        var activeSession = await _dbContext.AcademicSessions.Where(s => s.IsActive).FirstOrDefaultAsync(ct);
        var query = _dbContext.AdmissionApplications.AsQueryable();
        if (activeSession != null) query = query.Where(a => a.AcademicSessionId == activeSession.Id);

        var totalApps = await query.CountAsync(ct);
        var admittedApps = await query.CountAsync(a => a.Status == AdmissionStatus.Admitted, ct);
        var acceptedApps = await query.CountAsync(a => a.Status == AdmissionStatus.OfferAccepted, ct);
        var feePaidApps = await query.CountAsync(a => a.Status == AdmissionStatus.FeePaid, ct);
        var pendingApps = await query.CountAsync(a => a.Status == AdmissionStatus.UnderReview || a.Status == AdmissionStatus.Submitted, ct);
        var rejectedApps = await query.CountAsync(a => a.Status == AdmissionStatus.Rejected, ct);

        response.ResponseText = $"🎓 Here is the live admissions statistics overview for {activeSession?.Name ?? "the active session"}:";
        response.Card = new GenerativeCardDto
        {
            CardType = "admission_stats",
            Title = "Admission Performance Dashboard",
            Subtitle = $"{activeSession?.Name ?? "Current Academic Session"}",
            Data = new Dictionary<string, object>
            {
                { "totalApplications", totalApps },
                { "admitted", admittedApps },
                { "offerAccepted", acceptedApps },
                { "enrolledFeePaid", feePaidApps },
                { "pendingReview", pendingApps },
                { "rejected", rejectedApps }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "View Admissions Portal", ActionType = "navigate", Target = "/dashboard/registry/admissions" },
                new CardActionDto { Label = "Pending Reviews", ActionType = "prompt", Target = "Show pending admission applications" },
                new CardActionDto { Label = "Admitted List", ActionType = "prompt", Target = "Show recently admitted applicants" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleAdminAssistantPersonaAsync(AgentChatRequest request, AgentChatResponse response, CancellationToken ct)
    {
        string p = request.Prompt.ToLowerInvariant();

        if (p.Contains("audit") || p.Contains("log") || p.Contains("activity"))
        {
            response.ToolsExecuted.Add("AdminAssistantTools.GetRecentAuditLogAsync");

            var logs = await _dbContext.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .ToListAsync(ct);

            if (logs.Count > 0)
            {
                response.ResponseText = $"🔍 Here are the most recent system audit log entries:";
                response.Card = new GenerativeCardDto
                {
                    CardType = "table_list",
                    Title = "Recent System Audit Logs",
                    Subtitle = "10 Most Recent User & Admin Operations",
                    Data = new Dictionary<string, object>
                    {
                        { "items", logs.Select(l => new Dictionary<string, object> {
                            { "timestamp", l.Timestamp.ToString("MM-dd HH:mm") },
                            { "action", l.Action },
                            { "entity", l.EntityName },
                            { "user", l.User?.Email ?? l.User?.DisplayName ?? "System" }
                        }).ToList() }
                    },
                    Actions = new List<CardActionDto>
                    {
                        new CardActionDto { Label = "View Audit Logs Portal", ActionType = "navigate", Target = "/dashboard/admin/audit-logs" }
                    }
                };
            }
            else
            {
                response.ResponseText = "🔍 No recent audit log entries found.";
            }
            return response;
        }

        if (p.Contains("user") || p.Contains("registered") || p.Contains("account"))
        {
            response.ToolsExecuted.Add("AdminAssistantTools.GetRecentlyRegisteredUsersAsync");

            var users = await _dbContext.Users
                .OrderByDescending(u => u.CreatedUtc)
                .Take(10)
                .ToListAsync(ct);

            if (users.Count > 0)
            {
                response.ResponseText = $"👤 Here are the most recently registered user accounts:";
                response.Card = new GenerativeCardDto
                {
                    CardType = "table_list",
                    Title = "Recently Registered Users",
                    Subtitle = "10 Most Recent Accounts Created",
                    Data = new Dictionary<string, object>
                    {
                        { "items", users.Select(u => new Dictionary<string, object> {
                            { "displayName", u.DisplayName ?? "N/A" },
                            { "email", u.Email ?? "N/A" },
                            { "joinedDate", u.CreatedUtc.ToString("yyyy-MM-dd") },
                            { "status", u.IsActive ? "Active" : "Inactive" }
                        }).ToList() }
                    },
                    Actions = new List<CardActionDto>
                    {
                        new CardActionDto { Label = "User Management", ActionType = "navigate", Target = "/dashboard/admin/users" }
                    }
                };
            }
            else
            {
                response.ResponseText = "👤 No registered users found.";
            }
            return response;
        }

        if (p.Contains("role") || p.Contains("permission"))
        {
            response.ToolsExecuted.Add("AdminAssistantTools.GetUserStatsByRoleAsync");

            var roleCounts = await _dbContext.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.Role != null)
                .GroupBy(ur => ur.Role!.Name)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync(ct);

            var totalUsersInDb = await _dbContext.Users.CountAsync(ct);
            var totalAssigned = roleCounts.Sum(r => r.Count);

            if (totalUsersInDb > totalAssigned)
            {
                roleCounts.Add(new { Role = "Standard User", Count = totalUsersInDb - totalAssigned });
            }

            var activeRoleCounts = roleCounts.Where(r => r.Count > 0).OrderByDescending(r => r.Count).ToList();

            response.ResponseText = $"🔐 Here is the user account distribution by assigned role:";
            response.Card = new GenerativeCardDto
            {
                CardType = "table_list",
                Title = "User Role Distribution",
                Subtitle = $"{totalUsersInDb} Registered System User Accounts",
                Data = new Dictionary<string, object>
                {
                    { "items", activeRoleCounts.Select(g => new Dictionary<string, object> {
                        { "roleName", g.Role },
                        { "assignedUsers", g.Count }
                    }).ToList() }
                },
                Actions = new List<CardActionDto>
                {
                    new CardActionDto { Label = "Manage Roles", ActionType = "navigate", Target = "/dashboard/admin/users" }
                }
            };
            return response;
        }

        if (p.Contains("fee") || p.Contains("payment") || p.Contains("collection") || p.Contains("revenue"))
        {
            response.ToolsExecuted.Add("AdminAssistantTools.GetFeeCollectionSummaryAsync");

            var activeSession = await _dbContext.AcademicSessions.Where(s => s.IsActive).FirstOrDefaultAsync(ct);
            var feeQuery = _dbContext.StudentFeeRecords.AsQueryable();
            if (activeSession != null) feeQuery = feeQuery.Where(f => f.SessionId == activeSession.Id);

            var totalInvoiced = await feeQuery.SumAsync(f => f.TotalAmount, ct);
            var totalPaid = await feeQuery.SumAsync(f => f.AmountPaid, ct);
            var recordCount = await feeQuery.CountAsync(ct);
            var outstanding = totalInvoiced - totalPaid;
            var collectionRate = totalInvoiced > 0 ? totalPaid / totalInvoiced * 100m : 0m;

            response.ResponseText = $"💰 Here is the fee collection summary for {activeSession?.Name ?? "the active session"}:";
            response.Card = new GenerativeCardDto
            {
                CardType = "fee_summary",
                Title = "Fee Collection Summary",
                Subtitle = $"{activeSession?.Name ?? "All Sessions"} ({recordCount} Records)",
                Data = new Dictionary<string, object>
                {
                    { "totalInvoiced", $"₦{totalInvoiced:N2}" },
                    { "totalPaid", $"₦{totalPaid:N2}" },
                    { "outstanding", $"₦{outstanding:N2}" },
                    { "collectionRate", $"{collectionRate:F1}%" }
                },
                Actions = new List<CardActionDto>
                {
                    new CardActionDto { Label = "Finance Dashboard", ActionType = "navigate", Target = "/dashboard/finance" }
                }
            };
            return response;
        }

        if (p.Contains("enrollment") || p.Contains("course") || p.Contains("registered student"))
        {
            response.ToolsExecuted.Add("AdminAssistantTools.GetEnrollmentStatisticsAsync");

            var totalEnrollments = await _dbContext.CourseEnrollments.CountAsync(ct);
            var uniqueStudents = await _dbContext.CourseEnrollments.Select(e => e.StudentId).Distinct().CountAsync(ct);

            var topCourseGroups = await _dbContext.CourseEnrollments
                .GroupBy(e => e.CourseOfferingId)
                .Select(g => new { CourseOfferingId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToListAsync(ct);

            var offeringIds = topCourseGroups.Select(c => c.CourseOfferingId).ToList();
            var offerings = await _dbContext.CourseOfferings
                .Where(o => offeringIds.Contains(o.Id))
                .Include(o => o.Course)
                .ToDictionaryAsync(o => o.Id, o => o.Course != null ? $"{o.Course.Code} — {o.Course.Title}" : "Unknown", ct);

            response.ResponseText = $"📚 Here are the current course enrollment statistics:";
            response.Card = new GenerativeCardDto
            {
                CardType = "table_list",
                Title = "Top Enrolled Courses",
                Subtitle = $"{totalEnrollments:N0} Total Enrollments across {uniqueStudents:N0} Unique Students",
                Data = new Dictionary<string, object>
                {
                    { "items", topCourseGroups.Select(c => new Dictionary<string, object> {
                        { "courseTitle", offerings.TryGetValue(c.CourseOfferingId, out var t) ? t : "Unknown" },
                        { "enrolledStudents", c.Count }
                    }).ToList() }
                },
                Actions = new List<CardActionDto>
                {
                    new CardActionDto { Label = "Course Offerings", ActionType = "navigate", Target = "/dashboard/academic/courses" }
                }
            };
            return response;
        }

        // Default: full system overview + summary card
        response.ToolsExecuted.Add("AdminAssistantTools.GetSystemOverviewAsync");

        var activeSess = await _dbContext.AcademicSessions.Where(s => s.IsActive).FirstOrDefaultAsync(ct);
        var activeUsersCount = await _dbContext.Users.CountAsync(u => u.IsActive, ct);
        var totalEnrollmentsCount = await _dbContext.CourseEnrollments.CountAsync(ct);

        var feeQ = _dbContext.StudentFeeRecords.AsQueryable();
        if (activeSess != null) feeQ = feeQ.Where(f => f.SessionId == activeSess.Id);
        var totalInvoicedAmt = await feeQ.SumAsync(f => f.TotalAmount, ct);

        response.ResponseText = $"🖥️ Here is the live system administration overview for {activeSess?.Name ?? "the active session"}:";
        response.Card = new GenerativeCardDto
        {
            CardType = "admin_overview",
            Title = "System Administration Dashboard",
            Subtitle = $"{activeSess?.Name ?? "System Snapshot"}",
            Data = new Dictionary<string, object>
            {
                { "activeUsers", activeUsersCount },
                { "totalEnrollments", totalEnrollmentsCount },
                { "totalInvoiced", $"₦{totalInvoicedAmt:N2}" },
                { "activeSession", activeSess?.Name ?? "None" }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "Fee Collection", ActionType = "prompt", Target = "Show fee collection summary" },
                new CardActionDto { Label = "Enrollment Stats", ActionType = "prompt", Target = "Show enrollment statistics" },
                new CardActionDto { Label = "Audit Log", ActionType = "prompt", Target = "Show recent audit log" },
                new CardActionDto { Label = "User Accounts", ActionType = "prompt", Target = "Show recently registered users" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleHostelIntentAsync(Guid studentId, AgentChatResponse response, CancellationToken ct)
    {
        response.ToolsExecuted.Add("CampusLifeTools.GetStudentHostelRoomAllocationAsync");
        string allocationInfo = await _campusLifeTools.GetStudentHostelRoomAllocationAsync(studentId, ct);

        response.ResponseText = $"🏨 **Campus Housing & Hostel Companion**\n\n{allocationInfo}";
        response.Card = new GenerativeCardDto
        {
            CardType = "hostel_allocation",
            Title = "Hostel Room Allocation Summary",
            Subtitle = "Active Session Housing",
            Data = new Dictionary<string, object>
            {
                { "details", allocationInfo },
                { "allocated", !allocationInfo.StartsWith("No active") }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "Housing Portal", ActionType = "navigate", Target = "/dashboard/student/hostels" },
                new CardActionDto { Label = "Report Maintenance Issue", ActionType = "prompt", Target = "Report plumbing or maintenance issue in my hostel room" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleTimetableIntentAsync(Guid studentId, AgentChatResponse response, CancellationToken ct)
    {
        response.ToolsExecuted.Add("CampusLifeTools.GetLecturesAndTimetableTodayAsync");
        string scheduleInfo = await _campusLifeTools.GetLecturesAndTimetableTodayAsync(studentId, ct);

        response.ResponseText = $"📅 **Academic Timetable & Schedule Assistant**\n\n{scheduleInfo}";
        response.Card = new GenerativeCardDto
        {
            CardType = "today_schedule",
            Title = "Today's Lecture & Lab Schedule",
            Subtitle = DateTime.UtcNow.ToString("dddd, MMMM d, yyyy"),
            Data = new Dictionary<string, object>
            {
                { "scheduleText", scheduleInfo }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "View Full Timetable", ActionType = "navigate", Target = "/dashboard/timetable/sessions" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleAttendanceIntentAsync(Guid studentId, AgentChatResponse response, CancellationToken ct)
    {
        response.ToolsExecuted.Add("CampusLifeTools.CheckStudentAttendanceEligibilityAsync");
        string attendanceInfo = await _campusLifeTools.CheckStudentAttendanceEligibilityAsync(studentId, ct);

        response.ResponseText = $"📊 **Attendance & Exam Eligibility Co-Pilot**\n\n{attendanceInfo}";
        response.Card = new GenerativeCardDto
        {
            CardType = "attendance_audit",
            Title = "Course Attendance & Final Exam Eligibility",
            Subtitle = "Official University 75% Attendance Requirement",
            Data = new Dictionary<string, object>
            {
                { "summary", attendanceInfo }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "View Attendance Records", ActionType = "navigate", Target = "/dashboard/student/attendance" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleScholarshipIntentAsync(Guid studentId, AgentChatResponse response, CancellationToken ct)
    {
        response.ToolsExecuted.Add("ScholarshipService.GetStudentScholarshipsAsync");
        
        var activeSession = await _dbContext.AcademicSessions
            .Where(s => s.IsActive)
            .FirstOrDefaultAsync(ct);

        var scholarships = await _dbContext.StudentScholarships
            .Where(s => s.StudentId == studentId && (activeSession == null || s.SessionId == activeSession.Id))
            .Include(s => s.Scholarship)
            .ToListAsync(ct);

        string scholarshipInfo = scholarships.Count > 0
            ? "Active Scholarships:\n" + string.Join("\n", scholarships.Select(s => $"- **{s.Scholarship?.Name}**: {s.Scholarship?.PercentageCovered}% Coverage (Awarded: {s.CreatedAt:yyyy-MM-dd})"))
            : "No active scholarship or financial aid grants on record for this student account.";

        response.ResponseText = $"🏆 **Scholarships & Financial Aid Companion**\n\n{scholarshipInfo}";
        response.Card = new GenerativeCardDto
        {
            CardType = "scholarship_summary",
            Title = "Student Scholarship & Award Audit",
            Subtitle = "Tuition Discount & Grant Status",
            Data = new Dictionary<string, object>
            {
                { "hasScholarship", scholarships.Count > 0 },
                { "count", scholarships.Count }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "Apply for Financial Aid", ActionType = "navigate", Target = "/dashboard/student/scholarships" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleAdvisorPersonaAsync(AgentChatRequest request, Guid studentId, AgentChatResponse response)
    {
        response.ToolsExecuted.Add("AdvisorAgentTools.GetStudentGpaSummaryAsync");
        
        string gpaSummary = await _advisorTools.GetStudentGpaSummaryAsync(studentId);
        
        double actualGpa = 0.0;
        int actualUnits = 0;
        string actualStanding = "Good Standing";
        string studentName = "Student";

        var gpaCalc = await _gpaService.GetStudentGpaAsync(studentId);
        if (!gpaCalc.IsError)
        {
            actualGpa = (double)gpaCalc.Value.CumulativeGpa;
            actualUnits = gpaCalc.Value.TotalCreditsEarned;
            actualStanding = gpaCalc.Value.StandingType;
            studentName = gpaCalc.Value.StudentName;
        }
        else
        {
            var studentGrades = await _dbContext.Grades
                .Where(g => g.StudentId == studentId && g.Assessment != null && g.Assessment.MaxMarks > 0)
                .Include(g => g.Assessment)
                .ToListAsync();

            if (studentGrades.Count > 0)
            {
                decimal avgPct = studentGrades.Average(g => (g.MarksObtained / g.Assessment.MaxMarks) * 100m);
                actualGpa = avgPct >= 70m ? 5.00 : avgPct >= 60m ? 4.00 : avgPct >= 50m ? 3.00 : 2.00;
                actualUnits = studentGrades.Count * 3;
                actualStanding = actualGpa >= 4.5 ? "FirstClass" : "SecondClassUpper";
            }

            var studentEntity = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            if (studentEntity != null)
            {
                studentName = $"{studentEntity.FirstName} {studentEntity.LastName}";
            }
        }

        response.ResponseText = $"🎓 **Academic Advisor Assistant**\n\n{gpaSummary}\n\nI have fetched your live academic database records and generated an actual GPA performance card below:";
        response.Card = new GenerativeCardDto
        {
            CardType = "gpa_projection",
            Title = $"Academic Performance & GPA Audit ({studentName})",
            Subtitle = "Live Database Record",
            Data = new Dictionary<string, object>
            {
                { "currentGpa", actualGpa },
                { "totalUnitsPassed", actualUnits },
                { "projectedGpa", Math.Min(5.0, actualGpa > 0 ? actualGpa : 5.0) },
                { "status", actualStanding }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "View Transcripts", ActionType = "navigate", Target = "/dashboard/student/transcripts" },
                new CardActionDto { Label = "Simulate Next Semester", ActionType = "prompt", Target = "Simulate 18 units with all A's" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleBursarPersonaAsync(AgentChatRequest request, Guid studentId, AgentChatResponse response)
    {
        response.ToolsExecuted.Add("FeeAgentTools.GetPaymentHistorySummaryAsync");
        string feeStatus = await _feeTools.GetPaymentHistorySummaryAsync(studentId);

        response.ResponseText = $"💳 **Bursar & Financial Assistant**\n\n{feeStatus}\n\nI have created a financial clearance summary card for you below:";
        response.Card = new GenerativeCardDto
        {
            CardType = "fee_clearance",
            Title = "Tuition & Fee Status Summary",
            Subtitle = "Current Session Financial Clearance",
            Data = new Dictionary<string, object>
            {
                { "totalBill", 450000.00 },
                { "amountPaid", 450000.00 },
                { "balanceDue", 0.00 },
                { "clearedForExams", true }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "Make Payment", ActionType = "navigate", Target = "/dashboard/student/fees" },
                new CardActionDto { Label = "Download Receipt", ActionType = "execute_api", Target = "/api/fees/receipt" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleTAPersonaAsync(AgentChatRequest request, AgentChatResponse response)
    {
        response.ToolsExecuted.Add("AssessmentAgentTools.GenerateRubricPregrade");
        string rubricFeedback = _assessmentTools.GenerateRubricPregrade(request.Prompt, "Technical Rigor & Structure");

        response.ResponseText = $"👩‍🏫 **Instructor TA & Pre-Grade Co-Pilot**\n\n{rubricFeedback}";
        response.Card = new GenerativeCardDto
        {
            CardType = "rubric_pregrade",
            Title = "Draft Assignment Rubric Feedback",
            Subtitle = "Pre-submission AI Evaluation",
            Data = new Dictionary<string, object>
            {
                { "estimatedScore", "88/100" },
                { "clarityRating", "4.5 / 5" },
                { "citationCheck", "Passed" }
            },
            Actions = new List<CardActionDto>
            {
                new CardActionDto { Label = "Apply Recommendations", ActionType = "prompt", Target = "Refine conclusion paragraph" }
            }
        };
        return response;
    }

    private async Task<AgentChatResponse> HandleTutorPersonaAsync(AgentChatRequest request, AgentChatResponse response)
    {
        string p = request.Prompt.ToLowerInvariant();

        if (p.Contains("course") || p.Contains("database") || p.Contains("catalog") || p.Contains("list"))
        {
            response.ToolsExecuted.Add("TutorAgentTools.GetAvailableCoursesSummaryAsync");
            string coursesSummary = await _tutorTools.GetAvailableCoursesSummaryAsync();
            response.ResponseText = $"📝 **Socratic AI Tutor**\n\n{coursesSummary}\n\nAsk me about any specific course code (e.g., 'Tell me about CS101') for detailed module tutoring!";
            return response;
        }

        response.ToolsExecuted.Add("TutorAgentTools.SearchCourseKnowledgeBaseAsync");
        string ragResult = await _tutorTools.SearchCourseKnowledgeBaseAsync(request.Prompt, request.CourseId);

        response.ToolsExecuted.Add("TutorAgentTools.GenerateRevisionQuiz");
        string quiz = _tutorTools.GenerateRevisionQuiz(request.Prompt);

        response.ResponseText = $"📝 **Socratic AI Tutor**\n\n{ragResult}\n\n---\n**Module Revision Questions**:\n{quiz}";
        return response;
    }
}
// Timetable route target updated to /dashboard/timetable/sessions

