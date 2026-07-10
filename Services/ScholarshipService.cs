using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class ScholarshipService(LmsDbContext db) : IScholarshipService
{
    public async Task<ScholarshipDto> CreateScholarshipAsync(CreateScholarshipRequest req)
    {
        var s = new Scholarship
        {
            Name = req.Name,
            Description = req.Description,
            Type = req.Type,
            CoverageFlags = req.CoverageFlags,
            PercentageCovered = req.PercentageCovered,
            SponsorOrganizationId = req.SponsorOrganizationId,
            MinJambScore = req.MinJambScore,
            MaxJambScore = req.MaxJambScore,
            IsActive = req.IsActive
        };
        db.Scholarships.Add(s);
        await db.SaveChangesAsync();
        return MapToDto(s);
    }

    public async Task<ScholarshipDto> UpdateScholarshipAsync(Guid id, UpdateScholarshipRequest req)
    {
        var s = await db.Scholarships.FindAsync(id)
            ?? throw new KeyNotFoundException("Scholarship not found.");

        s.Name = req.Name;
        s.Description = req.Description;
        s.Type = req.Type;
        s.CoverageFlags = req.CoverageFlags;
        s.PercentageCovered = req.PercentageCovered;
        s.SponsorOrganizationId = req.SponsorOrganizationId;
        s.MinJambScore = req.MinJambScore;
        s.MaxJambScore = req.MaxJambScore;
        s.IsActive = req.IsActive;
        s.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToDto(s);
    }

    public async Task<IEnumerable<ScholarshipDto>> GetAllScholarshipsAsync(bool? activeOnly = null)
    {
        var q = db.Scholarships.Include(s => s.SponsorOrganization).AsQueryable();
        if (activeOnly.HasValue) q = q.Where(s => s.IsActive == activeOnly.Value);
        var res = await q.OrderBy(s => s.Name).ToListAsync();
        return res.Select(MapToDto);
    }

    public async Task<ScholarshipDto?> GetScholarshipByIdAsync(Guid id)
    {
        var s = await db.Scholarships.FindAsync(id);
        return s == null ? null : MapToDto(s);
    }

    public async Task<StudentScholarshipDto> AssignScholarshipAsync(AssignScholarshipRequest req)
    {
        var existing = await db.StudentScholarships
            .FirstOrDefaultAsync(ss => ss.StudentId == req.StudentId && ss.ScholarshipId == req.ScholarshipId && ss.SessionId == req.SessionId);
            
        if (existing != null)
            throw new InvalidOperationException("Scholarship is already assigned to this student for this session.");

        var ss = new StudentScholarship
        {
            StudentId = req.StudentId,
            ScholarshipId = req.ScholarshipId,
            SessionId = req.SessionId
        };
        
        db.StudentScholarships.Add(ss);
        await db.SaveChangesAsync();
        
        await db.Entry(ss).Reference(x => x.Scholarship).LoadAsync();
        return MapToStudentScholarshipDto(ss);
    }

    public async Task RemoveScholarshipAssignmentAsync(Guid id)
    {
        var ss = await db.StudentScholarships.FindAsync(id)
            ?? throw new KeyNotFoundException("Student scholarship assignment not found.");
        db.StudentScholarships.Remove(ss);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<StudentScholarshipDto>> GetStudentScholarshipsAsync(Guid studentId, Guid? sessionId = null)
    {
        var query = db.StudentScholarships
            .Include(ss => ss.Scholarship)
            .Include(ss => ss.Student)
            .Where(ss => ss.StudentId == studentId);

        if (sessionId.HasValue)
        {
            query = query.Where(ss => ss.SessionId == sessionId.Value);
        }

        var res = await query.ToListAsync();
        return res.Select(MapToStudentScholarshipDto);
    }
    
    public async Task<IEnumerable<StudentScholarshipDto>> GetAllStudentScholarshipsAsync(int limit = 100)
    {
        var res = await db.StudentScholarships
            .Include(ss => ss.Scholarship)
            .Include(ss => ss.Student)
            .OrderByDescending(ss => ss.CreatedAt)
            .Take(limit)
            .ToListAsync();
            
        return res.Select(MapToStudentScholarshipDto);
    }

    public async Task ApplyJambScholarshipsAsync(Guid studentId, Guid sessionId)
    {
        var student = await db.Students.FindAsync(studentId)
            ?? throw new KeyNotFoundException("Student not found.");
            
        if (!student.JambScore.HasValue) return;

        var score = student.JambScore.Value;

        // Find applicable JAMB scholarships based on score tiers
        var jambScholarships = await db.Scholarships
            .Where(s => s.IsActive && s.Type == ScholarshipType.JAMB)
            .ToListAsync();

        var applicable = jambScholarships.Where(s => 
            (!s.MinJambScore.HasValue || score >= s.MinJambScore.Value) &&
            (!s.MaxJambScore.HasValue || score <= s.MaxJambScore.Value)
        )
        .OrderByDescending(s => s.PercentageCovered)
        .Take(1)
        .ToList();

        // Get current assignments for JAMB
        var existingAssignments = await db.StudentScholarships
            .Include(ss => ss.Scholarship)
            .Where(ss => ss.StudentId == studentId && ss.SessionId == sessionId && ss.Scholarship.Type == ScholarshipType.JAMB)
            .ToListAsync();

        bool hasChanges = false;

        // Remove assignments that are no longer applicable
        var toRemove = existingAssignments.Where(ea => !applicable.Any(a => a.Id == ea.ScholarshipId)).ToList();
        if (toRemove.Any())
        {
            db.StudentScholarships.RemoveRange(toRemove);
            hasChanges = true;
        }

        // Add new assignments
        var toAdd = applicable.Where(a => !existingAssignments.Any(ea => ea.ScholarshipId == a.Id)).ToList();
        foreach (var s in toAdd)
        {
            db.StudentScholarships.Add(new StudentScholarship
            {
                StudentId = studentId,
                ScholarshipId = s.Id,
                SessionId = sessionId
            });
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    public async Task ApplyJambScholarshipsForAdmissionSessionAsync(Guid admissionSessionId)
    {
        // Get all students admitted in this session who have a JAMB score
        var students = await db.Students
            .Where(s => s.AcademicSessionId == admissionSessionId && s.JambScore.HasValue)
            .ToListAsync();

        if (!students.Any()) return;

        // Apply scholarship for each student for the admission session
        foreach (var student in students)
        {
            await ApplyJambScholarshipsAsync(student.Id, admissionSessionId);
        }
    }

    private static ScholarshipDto MapToDto(Scholarship s) => new(
        s.Id, s.Name, s.Description ?? "", s.Type, s.CoverageFlags, s.PercentageCovered,
        s.SponsorOrganizationId, s.SponsorOrganization?.Name, s.MinJambScore, s.MaxJambScore, s.IsActive, s.CreatedAt);

    private static StudentScholarshipDto MapToStudentScholarshipDto(StudentScholarship ss) => new(
        ss.Id, ss.StudentId, 
        ss.Student != null ? $"{ss.Student.FirstName} {ss.Student.LastName}" : null,
        ss.Student != null ? (ss.Student.JambRegistrationNumber ?? ss.Student.OfficialEmail) : null,
        ss.ScholarshipId, ss.SessionId, ss.CalculatedAmount, ss.CreatedAt,
        MapToDto(ss.Scholarship));
}
