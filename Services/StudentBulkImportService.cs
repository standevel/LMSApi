using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class StudentBulkImportService : BaseService, IStudentBulkImportService
{
    private readonly LmsDbContext _context;

    public StudentBulkImportService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<StudentImportResponse> ImportStudentsAsync(
        Stream csvStream,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(csvStream);
        while (!reader.EndOfStream)
        {
            lines.Add(await reader.ReadLineAsync(ct));
        }

        if (lines.Count < 2)
        {
            return new StudentImportResponse(
                bulkOperationId, 0, 0, 0, "Failed",
                Enumerable.Empty<StudentImportErrorDto>());
        }

        // Parse header row and build column index map
        var headerRow = ParseCsvLine(lines[0]);
        var columnMap = BuildColumnMap(headerRow);

        var errors = new List<StudentImportErrorDto>();
        var studentsToInsert = new List<Student>();
        int totalRows = lines.Count - 1; // exclude header

        // Resolve active session as fallback
        var activeSession = await _context.AcademicSessions
            .FirstOrDefaultAsync(s => s.IsActive, ct);

        for (int i = 1; i < lines.Count; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var rowNumber = i + 1;

            if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                var (student, error) = await MapRowToStudent(values, columnMap, defaultSessionId, activeSession, rowNumber, ct);
                if (error != null)
                {
                    errors.Add(error);
                }
                else if (student != null)
                {
                    studentsToInsert.Add(student);
                }
            }
        }

        // Batch insert valid students
        if (studentsToInsert.Count > 0)
        {
            await _context.Students.AddRangeAsync(studentsToInsert, ct);
            await _context.SaveChangesAsync(ct);
        }

        return new StudentImportResponse(
            bulkOperationId,
            totalRows,
            studentsToInsert.Count,
            errors.Count,
            errors.Count == 0 ? "Completed" : "CompletedWithErrors",
            errors);
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            map[normalized] = i;
        }
        return map;
    }

    private static string NormalizeHeader(string header)
    {
        return header.Trim().Replace(" ", "");
    }

    private static string? GetValue(string[] values, Dictionary<string, int> columnMap, string[] columnNames)
    {
        foreach (var name in columnNames)
        {
            if (columnMap.TryGetValue(name, out var index) && index < values.Length)
            {
                var val = values[index]?.Trim();
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
        }
        return null;
    }

    private async Task<(Student? Student, StudentImportErrorDto? Error)> MapRowToStudent(
        string[] values,
        Dictionary<string, int> columnMap,
        Guid? defaultSessionId,
        AcademicSession? defaultActiveSession,
        int rowNumber,
        CancellationToken ct)
    {
        var email = GetValue(values, columnMap, new[] { "Email", "EmailAddress" });
        var firstName = GetValue(values, columnMap, new[] { "FirstName" });
        var lastName = GetValue(values, columnMap, new[] { "Last Name", "LastName", "Surname", "Last Name Surname", "Last Name(Surname)" });
        var matricNumber = GetValue(values, columnMap, new[] { "Matric Number", "MatricNumber", "Matric" });
        var phoneNumber = GetValue(values, columnMap, new[] { "Phone Number", "PhoneNumber", "Phone" });
        var personalEmail = GetValue(values, columnMap, new[] { "Personal Email Address", "PersonalEmailAddress", "PersonalEmail" });
        var guardianPhone = GetValue(values, columnMap, new[] { "Guardian Phone", "GuardianPhone" });
        var guardianEmail = GetValue(values, columnMap, new[] { "Guardian Email", "GuardianEmail" });
        var levelName = GetValue(values, columnMap, new[] { "Level" });
        var programName = GetValue(values, columnMap, new[] { "Academic Program", "AcademicProgram" });
        var sponsorName = GetValue(values, columnMap, new[] { "Sponsor" });
        var jambNumber = GetValue(values, columnMap, new[] { "JAMB Numer", "JAMBNumber", "JambRegNumber" });
        var jambScoreStr = GetValue(values, columnMap, new[] { "JAMB Score", "JAMBScore" });
        var startTimeStr = GetValue(values, columnMap, new[] { "Start time", "StartTime" });
        var completionTimeStr = GetValue(values, columnMap, new[] { "Completion time", "CompletionTime" });
        var fullName = GetValue(values, columnMap, new[] { "Name" });

        if (string.IsNullOrEmpty(email))
        {
            return (null, new StudentImportErrorDto(rowNumber, null, "Email is required"));
        }

        if (!IsValidEmail(email))
        {
            return (null, new StudentImportErrorDto(rowNumber, email, "Invalid email format"));
        }

        // Resolve academic program
        AcademicProgram? program = null;
        if (!string.IsNullOrEmpty(programName))
        {
            program = await _context.Programs
                .FirstOrDefaultAsync(p => p.Name == programName || p.Code == programName, ct);
        }

        if (program == null && !string.IsNullOrEmpty(programName))
        {
            return (null, new StudentImportErrorDto(rowNumber, email, $"Academic program '{programName}' not found"));
        }

        // Resolve level
        AcademicLevel? level = null;
        if (!string.IsNullOrEmpty(levelName) && program != null)
        {
            level = await _context.Levels
                .FirstOrDefaultAsync(l => l.ProgramId == program.Id && l.Name == levelName, ct);
        }

        if (level == null && !string.IsNullOrEmpty(levelName))
        {
            return (null, new StudentImportErrorDto(rowNumber, email, $"Level '{levelName}' not found for program '{program?.Name}'"));
        }

        // Resolve sponsor
        SponsorOrganization? sponsor = null;
        if (!string.IsNullOrEmpty(sponsorName))
        {
            sponsor = await _context.SponsorOrganizations
                .FirstOrDefaultAsync(s => s.Name == sponsorName, ct);
        }

        // Parse dates
        DateTime? enrollmentDate = null;
        if (!string.IsNullOrEmpty(startTimeStr) && DateTime.TryParse(startTimeStr, out var parsedStart))
        {
            enrollmentDate = parsedStart;
        }

        DateTime? graduationDate = null;
        if (!string.IsNullOrEmpty(completionTimeStr) && DateTime.TryParse(completionTimeStr, out var parsedCompletion))
        {
            graduationDate = parsedCompletion;
        }

        // Parse JAMB score
        int? jambScore = null;
        if (!string.IsNullOrEmpty(jambScoreStr) && int.TryParse(jambScoreStr, out var parsedScore))
        {
            jambScore = parsedScore;
        }

        // Build student
        var student = new Student
        {
            OfficialEmail = email,
            PersonalEmail = !string.IsNullOrEmpty(personalEmail) ? personalEmail : email,
            FirstName = !string.IsNullOrEmpty(firstName) ? firstName : (fullName?.Split(' ').FirstOrDefault() ?? string.Empty),
            LastName = !string.IsNullOrEmpty(lastName) ? lastName : (fullName?.Split(' ').LastOrDefault() ?? string.Empty),
            StudentNumber = !string.IsNullOrEmpty(matricNumber) ? matricNumber : null,
            Phone = !string.IsNullOrEmpty(phoneNumber) ? phoneNumber : string.Empty,
            EmergencyContactPhone = guardianPhone,
            EmergencyContactEmail = guardianEmail,
            AcademicProgramId = program?.Id,
            LevelId = level?.Id,
            AcademicSessionId = defaultSessionId ?? (defaultActiveSession?.Id ?? Guid.Empty),
            Status = graduationDate.HasValue ? StudentStatus.Graduated : StudentStatus.Active,
            EnrollmentDate = enrollmentDate,
            GraduationDate = graduationDate,
            JambRegistrationNumber = jambNumber,
            JambScore = jambScore,
        };

        return (student, null);
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public interface IStudentBulkImportService
{
    Task<StudentImportResponse> ImportStudentsAsync(
        Stream csvStream,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default);
}
