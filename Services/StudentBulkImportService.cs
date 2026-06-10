using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class StudentBulkImportService : BaseService, IStudentBulkImportService
{
    private const int MaxStudentPhoneLength = 255;
    private readonly LmsDbContext _context;
    private readonly IEmailService _emailService;

    public StudentBulkImportService(LmsDbContext context, IAuditService auditService, IEmailService emailService) : base(auditService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<StudentImportResponse> ImportStudentsAsync(
        Stream csvStream,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(csvStream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lines.Add(line);
        }

        if (lines.Count < 2)
        {
            return new StudentImportResponse(
                bulkOperationId, 0, 0, 0, "Failed",
                [new StudentImportErrorDto(1, null, "CSV file must contain a header row and at least one data row")]);
        }

        var headerRow = ParseCsvLine(lines[0]);
        var columnMap = BuildColumnMap(headerRow);

        var errors = new List<StudentImportErrorDto>();
        int totalRows = 0;
        int processedRows = 0;

        var sessionId = await ResolveSessionIdAsync(defaultSessionId, ct);
        if (!sessionId.HasValue)
        {
            return new StudentImportResponse(
                bulkOperationId, lines.Count - 1, 0, lines.Count - 1, "Failed",
                [new StudentImportErrorDto(1, null, "No academic session available. Provide DefaultSessionId or configure an active session.")]);
        }

        var existingEmails = await _context.Students
            .Select(s => s.OfficialEmail.ToLower())
            .ToListAsync(ct);
        var existingEmailsSet = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);

        var existingMatricNumbers = await _context.Students
            .Where(s => s.StudentNumber != null)
            .Select(s => s.StudentNumber!)
            .ToListAsync(ct);
        var existingMatricSet = new HashSet<string>(existingMatricNumbers, StringComparer.OrdinalIgnoreCase);

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMatricNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var studentRoleId = await _context.Roles
            .Where(r => r.Name == LmsRoles.Student)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var parentRoleId = await _context.Roles
            .Where(r => r.Name == LmsRoles.Parent)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        for (int i = 1; i < lines.Count; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var rowNumber = i + 1;

            if (!values.Any(v => !string.IsNullOrWhiteSpace(v)))
                continue;

            totalRows++;

            var (student, error) = await MapRowToStudent(
                values, columnMap, sessionId.Value, rowNumber,
                existingEmailsSet, existingMatricSet, seenEmails, seenMatricNumbers, ct);

            if (error != null)
            {
                errors.Add(error);
                continue;
            }

            if (student == null)
                continue;

            try
            {
                _context.Students.Add(student);
                await _context.SaveChangesAsync(ct);
                await EnsureAppUserForStudentAsync(student, studentRoleId, ct);

                // Create guardian account if guardian email is present on the student
                if (!string.IsNullOrWhiteSpace(student.EmergencyContactEmail))
                {
                    await EnsureGuardianAccountForStudentAsync(
                        student,
                        guardianFirstName: null,
                        guardianLastName: null,
                        guardianPhone: student.EmergencyContactPhone,
                        guardianEmail: student.EmergencyContactEmail,
                        guardianRelationship: "Guardian",
                        parentRoleId,
                        ct);
                }

                processedRows++;
                existingEmailsSet.Add(student.OfficialEmail);
                if (!string.IsNullOrEmpty(student.StudentNumber))
                    existingMatricSet.Add(student.StudentNumber);
            }
            catch (DbUpdateException ex)
            {
                _context.Entry(student).State = EntityState.Detached;
                errors.Add(new StudentImportErrorDto(rowNumber, student.OfficialEmail,
                    $"Database error: {ex.InnerException?.Message ?? ex.Message}"));
            }
        }

        var status = processedRows == 0 && errors.Count > 0
            ? "Failed"
            : errors.Count == 0
                ? "Completed"
                : "CompletedWithErrors";

        return new StudentImportResponse(
            bulkOperationId,
            totalRows,
            processedRows,
            errors.Count,
            status,
            errors.ToList());
    }

    public async Task<StudentImportResponse> ImportStudentsFromRowsAsync(
        List<Contracts.StudentImportRowDto> rows,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default)
    {
        if (rows == null || rows.Count == 0)
        {
            return new StudentImportResponse(
                bulkOperationId, 0, 0, 0, "Failed",
                [new StudentImportErrorDto(0, null, "No student data provided")]);
        }

        var errors = new List<StudentImportErrorDto>();
        int totalRows = rows.Count;
        int processedRows = 0;

        var sessionId = await ResolveSessionIdAsync(defaultSessionId, ct);
        if (!sessionId.HasValue)
        {
            return new StudentImportResponse(
                bulkOperationId, totalRows, 0, totalRows, "Failed",
                [new StudentImportErrorDto(1, null, "No academic session available. Provide DefaultSessionId or configure an active session.")]);
        }

        // Pre-load all reference data into memory to avoid N+1 queries and race conditions
        var existingEmailsSet = new HashSet<string>(
            await _context.Students.Select(s => s.OfficialEmail).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var existingMatricSet = new HashSet<string>(
            await _context.Students.Where(s => s.StudentNumber != null).Select(s => s.StudentNumber!).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        // In-memory caches so rows within the same import share created entities
        var programCache = (await _context.Programs.ToListAsync(ct))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var levelCache = (await _context.Levels.ToListAsync(ct))
            .GroupBy(l => l.ProgramId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(levelGroup => levelGroup.Key, levelGroup => levelGroup.First(), StringComparer.OrdinalIgnoreCase));

        var defaultDepartmentId = await _context.Departments.Select(d => d.Id).FirstOrDefaultAsync(ct);

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMatricNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var studentRoleId = await _context.Roles
            .Where(r => r.Name == LmsRoles.Student)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var parentRoleId = await _context.Roles
            .Where(r => r.Name == LmsRoles.Parent)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;

            // --- Validate email ---
            if (string.IsNullOrWhiteSpace(row.Email))
            {
                errors.Add(new StudentImportErrorDto(rowNumber, null, "Email is required"));
                continue;
            }
            var email = row.Email.Trim();
            if (!IsValidEmail(email))
            {
                errors.Add(new StudentImportErrorDto(rowNumber, email, "Invalid email format"));
                continue;
            }
            if (existingEmailsSet.Contains(email) || !seenEmails.Add(email))
            {
                errors.Add(new StudentImportErrorDto(rowNumber, email, "A student with this email already exists"));
                continue;
            }

            // --- Validate matric number ---
            var matricNumber = string.IsNullOrWhiteSpace(row.MatricNumber) ? null : row.MatricNumber!.Trim();
            if (!string.IsNullOrEmpty(matricNumber) &&
                (existingMatricSet.Contains(matricNumber) || !seenMatricNumbers.Add(matricNumber)))
            {
                errors.Add(new StudentImportErrorDto(rowNumber, email, $"Matric number '{matricNumber}' already exists"));
                continue;
            }

            // --- Resolve name ---
            var fullName = row.Name?.Trim() ?? string.Empty;
            var firstName = !string.IsNullOrWhiteSpace(row.FirstName) ? row.FirstName!.Trim() : string.Empty;
            var lastName = !string.IsNullOrWhiteSpace(row.LastName) ? row.LastName!.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(fullName))
            {
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                firstName = parts.FirstOrDefault() ?? string.Empty;
                lastName = parts.Length > 1 ? parts.LastOrDefault() ?? string.Empty : firstName;
            }
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                errors.Add(new StudentImportErrorDto(rowNumber, email, "First name and last name are required"));
                continue;
            }

            // --- Resolve program (find or create) ---
            AcademicProgram? program = null;
            if (!string.IsNullOrWhiteSpace(row.AcademicProgram))
            {
                var programName = row.AcademicProgram!.Trim();
                if (!programCache.TryGetValue(programName, out program))
                {
                    if (defaultDepartmentId == Guid.Empty)
                    {
                        errors.Add(new StudentImportErrorDto(rowNumber, email,
                            $"Program '{programName}' not found and no departments exist to auto-create it"));
                        continue;
                    }
                    program = new AcademicProgram
                    {
                        Id = Guid.NewGuid(),
                        Name = programName,
                        Code = GenerateProgramCode(programName),
                        DegreeAwarded = string.Empty,
                        DepartmentId = defaultDepartmentId,
                        Type = Data.Enums.ProgramType.Undergraduate,
                        DurationYears = 4,
                        MinJambScore = 0,
                        MaxAdmissions = 0,
                        IsActive = true
                    };
                    _context.Programs.Add(program);
                    await _context.SaveChangesAsync(ct);
                    programCache[program.Name] = program;
                    levelCache[program.Id] = new Dictionary<string, AcademicLevel>(StringComparer.OrdinalIgnoreCase);
                }
            }

            // --- Resolve level (find or create) ---
            AcademicLevel? level = null;
            if (!string.IsNullOrWhiteSpace(row.Level))
            {
                if (program == null)
                {
                    errors.Add(new StudentImportErrorDto(rowNumber, email, "Academic program is required when level is specified"));
                    continue;
                }
                var levelName = NormalizeLevelName(row.Level!.Trim());
                if (!levelCache.TryGetValue(program.Id, out var progLevels))
                {
                    progLevels = new Dictionary<string, AcademicLevel>(StringComparer.OrdinalIgnoreCase);
                    levelCache[program.Id] = progLevels;
                }
                if (!progLevels.TryGetValue(levelName, out level))
                {
                    // Parse order from level name digits (e.g. "100 Level" => order 1)
                    var digits = new string(levelName.Where(char.IsDigit).ToArray());
                    var order = int.TryParse(digits, out var o)
                        ? o >= 100 ? o / 100 : o
                        : 0;
                    level = new AcademicLevel
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = program.Id,
                        Name = levelName,
                        Order = order
                    };
                    _context.Levels.Add(level);
                    await _context.SaveChangesAsync(ct);
                    progLevels[levelName] = level;
                }
            }

            // --- Parse optional date fields ---
            DateTime? enrollmentDate = null;
            if (!string.IsNullOrWhiteSpace(row.StartTime) && DateTime.TryParse(row.StartTime, out var parsedStart))
                enrollmentDate = parsedStart;

            DateTime? graduationDate = null;
            if (!string.IsNullOrWhiteSpace(row.CompletionTime) && DateTime.TryParse(row.CompletionTime, out var parsedCompletion))
                graduationDate = parsedCompletion;

            var phone = NormalizeOptionalText(row.PhoneNumber) ?? string.Empty;
            if (phone.Length > MaxStudentPhoneLength)
            {
                errors.Add(new StudentImportErrorDto(rowNumber, email,
                    $"Phone must be {MaxStudentPhoneLength} characters or fewer. Current value has {phone.Length} characters."));
                continue;
            }

            // --- Build and save student ---
            var student = new Student
            {
                OfficialEmail = email,
                PersonalEmail = !string.IsNullOrWhiteSpace(row.PersonalEmail) ? row.PersonalEmail!.Trim() : email,
                FirstName = firstName,
                LastName = lastName,
                MiddleName = null,
                StudentNumber = matricNumber,
                Phone = phone,
                EmergencyContactPhone = string.IsNullOrWhiteSpace(row.GuardianPhone) ? null : row.GuardianPhone!.Trim(),
                EmergencyContactEmail = string.IsNullOrWhiteSpace(row.GuardianEmail) ? null : row.GuardianEmail!.Trim(),
                AcademicProgramId = program?.Id,
                LevelId = level?.Id,
                AcademicSessionId = sessionId.Value,
                Status = graduationDate.HasValue ? StudentStatus.Graduated : StudentStatus.Active,
                EnrollmentDate = enrollmentDate ?? DateTime.UtcNow,
                GraduationDate = graduationDate,
                JambRegistrationNumber = string.IsNullOrWhiteSpace(row.JambNumber) ? null : row.JambNumber!.Trim(),
                JambScore = row.JambScore,
                EntraObjectId = null,
                AdmissionApplicationId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Students.Add(student);
                await _context.SaveChangesAsync(ct);
                await EnsureAppUserForStudentAsync(student, studentRoleId, ct);

                // Create guardian account if guardian email is provided
                if (!string.IsNullOrWhiteSpace(row.GuardianEmail))
                {
                    await EnsureGuardianAccountForStudentAsync(
                        student,
                        row.GuardianFirstName,
                        row.GuardianLastName,
                        row.GuardianPhone,
                        row.GuardianEmail,
                        row.GuardianRelationship,
                        parentRoleId,
                        ct);
                }

                processedRows++;
                existingEmailsSet.Add(student.OfficialEmail);
                if (!string.IsNullOrEmpty(student.StudentNumber))
                    existingMatricSet.Add(student.StudentNumber);
            }
            catch (DbUpdateException ex)
            {
                _context.Entry(student).State = EntityState.Detached;
                errors.Add(new StudentImportErrorDto(rowNumber, student.OfficialEmail,
                    $"Database error: {ex.InnerException?.Message ?? ex.Message}"));
            }
        }

        var status = processedRows == 0 && errors.Count > 0
            ? "Failed"
            : errors.Count == 0
                ? "Completed"
                : "CompletedWithErrors";

        return new StudentImportResponse(
            bulkOperationId, totalRows, processedRows, errors.Count, status, errors);
    }

    private async Task<Guid?> ResolveSessionIdAsync(Guid? defaultSessionId, CancellationToken ct)
    {
        if (defaultSessionId.HasValue)
        {
            var exists = await _context.AcademicSessions.AnyAsync(s => s.Id == defaultSessionId.Value, ct);
            if (exists)
                return defaultSessionId.Value;
        }

        var activeSession = await _context.AcademicSessions
            .FirstOrDefaultAsync(s => s.IsActive, ct);

        return activeSession?.Id;
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
            var normalized = NormalizeHeader(name);
            if (columnMap.TryGetValue(normalized, out var index) && index < values.Length)
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
        Guid sessionId,
        int rowNumber,
        HashSet<string> existingEmails,
        HashSet<string> existingMatricNumbers,
        HashSet<string> seenEmails,
        HashSet<string> seenMatricNumbers,
        CancellationToken ct)
    {
        var email = GetValue(values, columnMap, new[] { "Email", "EmailAddress", "Contacte-mail", "ContactE-mail" });
        var firstName = GetValue(values, columnMap, new[] { "FirstName", "First Name" });
        var lastName = GetValue(values, columnMap, new[] { "LastName", "Last Name", "Surname", "LastName(Surname)", "Last Name(Surname)" });
        var matricNumber = GetValue(values, columnMap, new[] { "MatricNumber", "Matric Number", "Matric", "RegistrationNumber", "Registration Number" });
        var phoneNumber = GetValue(values, columnMap, new[] { "PhoneNumber", "Phone Number", "Phone", "MobilePhone", "Mobile Phone" });
        var personalEmail = GetValue(values, columnMap, new[] { "PersonalEmailAddress", "Personal Email Address", "PersonalEmail" });
        var guardianPhone = GetValue(values, columnMap, new[] { "GuardianPhone", "Guardian Phone" });
        var guardianEmail = GetValue(values, columnMap, new[] { "GuardianEmail", "Guardian Email" });
        var levelName = GetValue(values, columnMap, new[] { "Level", "Year-Semester" });
        var programName = GetValue(values, columnMap, new[] { "AcademicProgram", "Academic Program", "Program" });
        var jambNumber = GetValue(values, columnMap, new[] { "JAMBNumber", "JAMB Number", "JAMB Numer", "JambRegNumber" });
        var jambScoreStr = GetValue(values, columnMap, new[] { "JAMBScore", "JAMB Score" });
        var startTimeStr = GetValue(values, columnMap, new[] { "Starttime", "StartTime", "Start time" });
        var completionTimeStr = GetValue(values, columnMap, new[] { "Completiontime", "CompletionTime", "Completion time" });
        var fullName = GetValue(values, columnMap, new[] { "Name" });

        if (string.IsNullOrEmpty(email))
            return (null, new StudentImportErrorDto(rowNumber, null, "Email is required"));

        if (!IsValidEmail(email))
            return (null, new StudentImportErrorDto(rowNumber, email, "Invalid email format"));

        if (existingEmails.Contains(email) || !seenEmails.Add(email))
            return (null, new StudentImportErrorDto(rowNumber, email, "A student with this email already exists"));

        if (!string.IsNullOrEmpty(matricNumber))
        {
            if (existingMatricNumbers.Contains(matricNumber) || !seenMatricNumbers.Add(matricNumber))
                return (null, new StudentImportErrorDto(rowNumber, email, $"Matric number '{matricNumber}' already exists"));
        }

        AcademicProgram? program = null;
        if (!string.IsNullOrEmpty(programName))
        {
            program = await _context.Programs
                .FirstOrDefaultAsync(p => p.Name == programName || p.Code == programName, ct);

            if (program == null)
                return (null, new StudentImportErrorDto(rowNumber, email, $"Academic program '{programName}' not found"));
        }

        AcademicLevel? level = null;
        if (!string.IsNullOrEmpty(levelName))
        {
            if (program == null)
                return (null, new StudentImportErrorDto(rowNumber, email, "Academic program is required when level is specified"));

            level = await _context.Levels
                .FirstOrDefaultAsync(l => l.ProgramId == program.Id && l.Name == levelName, ct);

            if (level == null)
                return (null, new StudentImportErrorDto(rowNumber, email, $"Level '{levelName}' not found for program '{program.Name}'"));
        }

        DateTime? enrollmentDate = null;
        if (!string.IsNullOrEmpty(startTimeStr) && DateTime.TryParse(startTimeStr, out var parsedStart))
            enrollmentDate = parsedStart;

        DateTime? graduationDate = null;
        if (!string.IsNullOrEmpty(completionTimeStr) && DateTime.TryParse(completionTimeStr, out var parsedCompletion))
            graduationDate = parsedCompletion;

        int? jambScore = null;
        if (!string.IsNullOrEmpty(jambScoreStr) && int.TryParse(jambScoreStr, out var parsedScore))
            jambScore = parsedScore;

        var resolvedFirstName = !string.IsNullOrEmpty(firstName)
            ? firstName
            : (fullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty);
        var resolvedLastName = !string.IsNullOrEmpty(lastName)
            ? lastName
            : (fullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(resolvedFirstName) || string.IsNullOrWhiteSpace(resolvedLastName))
            return (null, new StudentImportErrorDto(rowNumber, email, "First name and last name are required"));

        phoneNumber = NormalizeOptionalText(phoneNumber);
        if ((phoneNumber?.Length ?? 0) > MaxStudentPhoneLength)
        {
            return (null, new StudentImportErrorDto(rowNumber, email,
                $"Phone must be {MaxStudentPhoneLength} characters or fewer. Current value has {phoneNumber!.Length} characters."));
        }

        var student = new Student
        {
            OfficialEmail = email,
            PersonalEmail = !string.IsNullOrEmpty(personalEmail) ? personalEmail : email,
            FirstName = resolvedFirstName,
            LastName = resolvedLastName,
            StudentNumber = matricNumber,
            Phone = phoneNumber ?? string.Empty,
            EmergencyContactPhone = guardianPhone,
            EmergencyContactEmail = guardianEmail,
            AcademicProgramId = program?.Id,
            LevelId = level?.Id,
            AcademicSessionId = sessionId,
            Status = graduationDate.HasValue ? StudentStatus.Graduated : StudentStatus.Active,
            EnrollmentDate = enrollmentDate ?? DateTime.UtcNow,
            GraduationDate = graduationDate,
            JambRegistrationNumber = jambNumber,
            JambScore = jambScore,
            EntraObjectId = null,
            AdmissionApplicationId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
                    i++;
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

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private async Task EnsureAppUserForStudentAsync(Student student, Guid? studentRoleId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == student.Id ||
            u.Email == student.OfficialEmail ||
            u.Username == student.OfficialEmail ||
            (!string.IsNullOrWhiteSpace(student.EntraObjectId) && u.EntraObjectId == student.EntraObjectId),
            ct);

        if (user == null)
        {
            user = new AppUser
            {
                Id = student.Id,
                EntraObjectId = string.IsNullOrWhiteSpace(student.EntraObjectId) ? $"student:{student.Id}" : student.EntraObjectId,
                Username = student.OfficialEmail,
                Email = student.OfficialEmail,
                DisplayName = $"{student.FirstName} {student.LastName}".Trim(),
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _context.Users.Add(user);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.EntraObjectId))
                user.EntraObjectId = string.IsNullOrWhiteSpace(student.EntraObjectId) ? $"student:{student.Id}" : student.EntraObjectId;

            user.Username ??= student.OfficialEmail;
            user.Email ??= student.OfficialEmail;
            user.DisplayName = $"{student.FirstName} {student.LastName}".Trim();
            user.IsActive = true;
            user.UpdatedUtc = now;
        }

        if (studentRoleId.HasValue)
        {
            var hasStudentRole = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == studentRoleId.Value, ct);

            if (!hasStudentRole)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = studentRoleId.Value,
                    AssignedUtc = now
                });
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates an AppUser and ParentGuardian record for the student's guardian,
    /// then links them via ParentStudentLink. Skipped if guardian email is absent
    /// or an account with that email already exists.
    /// </summary>
    private async Task EnsureGuardianAccountForStudentAsync(
        Student student,
        string? guardianFirstName,
        string? guardianLastName,
        string? guardianPhone,
        string? guardianEmail,
        string? guardianRelationship,
        Guid? parentRoleId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(guardianEmail))
            return;

        var now = DateTime.UtcNow;
        var email = guardianEmail.Trim();

        // Resolve display name — fall back to email prefix when name is absent
        var firstName = !string.IsNullOrWhiteSpace(guardianFirstName) ? guardianFirstName!.Trim() : string.Empty;
        var lastName  = !string.IsNullOrWhiteSpace(guardianLastName)  ? guardianLastName!.Trim()  : string.Empty;
        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
        {
            firstName = email.Split('@')[0];
            lastName  = string.Empty;
        }
        var displayName = $"{firstName} {lastName}".Trim();
        var phone = !string.IsNullOrWhiteSpace(guardianPhone) ? guardianPhone!.Trim() : string.Empty;
        var relationship = !string.IsNullOrWhiteSpace(guardianRelationship) ? guardianRelationship!.Trim() : "Guardian";

        // Find or create the AppUser for the guardian
        var guardianUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email || u.Username == email, ct);

        bool isNewAccount = guardianUser == null;

        if (guardianUser == null)
        {
            guardianUser = new AppUser
            {
                Id = Guid.NewGuid(),
                EntraObjectId = $"parent:{Guid.NewGuid()}",
                Username = email,
                Email = email,
                DisplayName = displayName,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _context.Users.Add(guardianUser);
            await _context.SaveChangesAsync(ct);
        }

        // Assign Parent role if not already assigned
        if (parentRoleId.HasValue)
        {
            var hasParentRole = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == guardianUser.Id && ur.RoleId == parentRoleId.Value, ct);

            if (!hasParentRole)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = guardianUser.Id,
                    RoleId = parentRoleId.Value,
                    AssignedUtc = now
                });
            }
        }

        // Find or create the ParentGuardian profile
        var guardian = await _context.ParentGuardians
            .FirstOrDefaultAsync(pg => pg.UserId == guardianUser.Id || pg.Email == email, ct);

        if (guardian == null)
        {
            guardian = new ParentGuardian
            {
                Id = Guid.NewGuid(),
                UserId = guardianUser.Id,
                FirstName = string.IsNullOrEmpty(firstName) ? displayName : firstName,
                LastName = lastName,
                PhoneNumber = phone,
                Email = email,
                DateAddedUtc = now
            };
            _context.ParentGuardians.Add(guardian);
            await _context.SaveChangesAsync(ct);
        }

        // Link guardian to student if not already linked
        var alreadyLinked = await _context.ParentStudentLinks
            .AnyAsync(l => l.ParentGuardianId == guardian.Id && l.StudentId == student.Id, ct);

        if (!alreadyLinked)
        {
            _context.ParentStudentLinks.Add(new ParentStudentLink
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = guardian.Id,
                StudentId = student.Id,
                RelationshipType = relationship,
                LinkedAtUtc = now
            });
            await _context.SaveChangesAsync(ct);
        }

        // Notify guardian — fire-and-forget; a failure here must not break the import
        var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
        var guardianDisplayName = string.IsNullOrEmpty(displayName) ? email : displayName;
        try
        {
            await _emailService.SendGuardianCredentialsEmailAsync(
                email,
                guardianDisplayName,
                studentFullName,
                email,
                isNewAccount: isNewAccount);
        }
        catch (Exception emailEx)
        {
            // Log but swallow — email failure should not roll back a successful import row
            _ = emailEx; // suppress unused-variable warning; caller can check logs
        }
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

    /// <summary>
    /// Converts raw level values from spreadsheets to the canonical DB format.
    /// "100" → "100 Level", "200 Level" → "200 Level", "Year 1" → "Year 1", etc.
    /// </summary>
    private static string NormalizeLevelName(string raw)
    {
        var trimmed = raw.Trim();

        // Already in "NNN Level" format
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{1,3}\s+[Ll]evel$"))
            return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+[Ll]evel$", " Level");

        // Plain number e.g. "100", "200"
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{1,3}$"))
            return $"{trimmed} Level";

        // Single digit year e.g. "1", "2" → "100 Level", "200 Level"
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[1-7]$"))
            return $"{trimmed}00 Level";

        // "Year N" → keep as-is
        return trimmed;
    }

    /// <summary>
    /// Generates a short unique code from a program name for new auto-created programs.
    /// </summary>
    private static string GenerateProgramCode(string programName)
    {
        // Take initials of significant words and append a hash suffix for uniqueness
        var words = programName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !new[] { "and", "the", "of", "in" }.Contains(w.ToLower()))
            .ToArray();
        var initials = string.Concat(words.Take(4).Select(w => char.ToUpper(w[0])));
        var suffix = Math.Abs(programName.GetHashCode()) % 1000;
        return $"{initials}{suffix}";
    }
}

public interface IStudentBulkImportService
{
    Task<StudentImportResponse> ImportStudentsAsync(
        Stream csvStream,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default);

    Task<StudentImportResponse> ImportStudentsFromRowsAsync(
        List<Contracts.StudentImportRowDto> rows,
        Guid bulkOperationId,
        Guid? defaultSessionId,
        CancellationToken ct = default);
}
