using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class ParentPortalService : BaseService, IParentPortalService
{
    private readonly LmsDbContext _context;

    public ParentPortalService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<List<ParentGuardianDto>>> GetLinkedStudentsAsync(Guid parentId, CancellationToken ct = default)
    {
        var parentGuardian = await _context.ParentGuardians
            .Include(pg => pg.AppUser)
            .FirstOrDefaultAsync(pg => pg.Id == parentId, ct);

        if (parentGuardian == null)
        {
            return Error.NotFound("ParentGuardian.NotFound", "Parent guardian not found.");
        }

        var links = await _context.ParentStudentLinks
            .Include(psl => psl.Student)
            .Include(psl => psl.ParentGuardian)
            .Where(psl => psl.ParentGuardianId == parentId)
            .ToListAsync(ct);

        return links.Select(psl => new ParentGuardianDto(
            psl.ParentGuardian.Id,
            $"{psl.ParentGuardian.FirstName} {psl.ParentGuardian.LastName}",
            psl.ParentGuardian.Email ?? string.Empty,
            psl.ParentGuardian.PhoneNumber,
            psl.RelationshipType,
            psl.ParentGuardian.DateAddedUtc)).ToList();
    }

    public async Task<ErrorOr<StudentProgressDto>> GetStudentProgressAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        // In a real implementation, we would calculate actual progress
        // For now, returning placeholder data
        var courseProgress = new List<CourseProgressDto>();

        return new StudentProgressDto(
            student.Id,
            !string.IsNullOrWhiteSpace(student.FirstName) || !string.IsNullOrWhiteSpace(student.LastName) ? $"{student.FirstName} {student.LastName}".Trim() : "Unknown",
            3.5m, // Placeholder GPA
            45,   // Placeholder credits earned
            120,  // Placeholder total credits required
            courseProgress);
    }

    public async Task<ErrorOr<StudentGradesDto>> GetStudentGradesAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        // In a real implementation, we would get actual grades
        // For now, returning placeholder data
        var grades = new List<StudentGradeDto>();

        return new StudentGradesDto(
            student.Id,
            !string.IsNullOrWhiteSpace(student.FirstName) || !string.IsNullOrWhiteSpace(student.LastName) ? $"{student.FirstName} {student.LastName}".Trim() : "Unknown",
            grades);
    }

    public async Task<ErrorOr<Deleted>> SendMessageToStudentAsync(Guid studentId, Guid parentId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("InvalidInput", "Message content is required.");
        }

        var student = await _context.Students.FindAsync(new object[] { studentId }, ct);
        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        var parent = await _context.ParentGuardians.FindAsync(new object[] { parentId }, ct);
        if (parent == null)
        {
            return Error.NotFound("ParentGuardian.NotFound", "Parent guardian not found.");
        }

        // Verify that the parent is actually linked to this student
        var link = await _context.ParentStudentLinks
            .FirstOrDefaultAsync(psl => psl.ParentGuardianId == parentId && psl.StudentId == studentId, ct);

        if (link == null)
        {
            return Error.Forbidden("AccessDenied", "Parent is not linked to this student.");
        }

        // In a real implementation, we would create and save a message entity
        // For now, just return success
        await LogActionAsync("SendMessageToStudent", "ParentMessage", Guid.NewGuid().ToString(),
            $"Parent {parentId} sent message to student {studentId}", ct);

        return Result.Deleted;
    }
}