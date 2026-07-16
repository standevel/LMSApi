using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
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
            IsActive = true
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
        var ss = new StudentScholarship
        {
            StudentId = req.StudentId,
            ScholarshipId = req.ScholarshipId,
            SessionId = req.SessionId
        };
        db.StudentScholarships.Add(ss);
        await db.SaveChangesAsync();
        
        // Reload with includes
        var reloaded = await db.StudentScholarships
            .Include(x => x.Student)
            .Include(x => x.Scholarship)
            .ThenInclude(s => s.SponsorOrganization)
            .FirstAsync(x => x.Id == ss.Id);
            
        return MapToStudentScholarshipDto(reloaded);
    }

    public async Task RemoveScholarshipAssignmentAsync(Guid id)
    {
        var ss = await db.StudentScholarships.FindAsync(id)
            ?? throw new KeyNotFoundException("Assignment not found.");
            
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
            .Include(ss => ss.Student)
            .Include(ss => ss.Scholarship)
            .ThenInclude(s => s.SponsorOrganization)
            .OrderByDescending(ss => ss.CreatedAt)
            .Take(limit)
            .ToListAsync();
            
        return res.Select(MapToStudentScholarshipDto);
    }

    public static int ConvertDirectEntryToJambScore(DirectEntryQualification qual, decimal? points, string? gradeStr)
    {
        // 1. Points-based scale (A-Level, IJMB, IB, Cambridge)
        if (points.HasValue && (
            qual == DirectEntryQualification.ALevel ||
            qual == DirectEntryQualification.IJMB ||
            qual == DirectEntryQualification.IB ||
            qual == DirectEntryQualification.CambridgeAdvanced ||
            qual == DirectEntryQualification.AdvancedAdvanced))
        {
            var pts = points.Value;
            if (pts >= 15) return 350;
            if (pts >= 14) return 330;
            if (pts >= 13) return 310;
            if (pts >= 12) return 290;
            if (pts >= 11) return 270;
            if (pts >= 10) return 250;
            if (pts >= 9) return 230;
            if (pts >= 8) return 210;
            if (pts >= 7) return 190;
            if (pts >= 6) return 180;
            return 160;
        }

        // 2. Class-based/grade-based qualifications (HND, ND, Diploma, BTEC)
        if (!string.IsNullOrEmpty(gradeStr))
        {
            var g = gradeStr.Replace(" ", "").Replace("*", "").ToLowerInvariant();

            if (g.Contains("firstclass") || g.Contains("distinction") || g.Contains("first"))
                return 340;
            if (g.Contains("secondclassupper") || g.Contains("uppercredit") || g.Contains("merit") || g.Contains("upper"))
                return 290;
            if (g.Contains("secondclasslower") || g.Contains("lowercredit") || g.Contains("lower"))
                return 240;
            if (g.Contains("thirdclass") || g.Contains("third"))
                return 210;
            if (g.Contains("pass"))
                return 180;
        }

        // 3. Fallback based on points directly if none of the above matched
        if (points.HasValue)
        {
            var pts = points.Value;
            // ND/HND CGPA out of 4.0/5.0
            if (pts >= 4.5m) return 340;
            if (pts >= 3.5m) return 340;
            if (pts >= 3.0m) return 290;
            if (pts >= 2.5m) return 240;
            if (pts >= 2.0m) return 200;
        }

        return 0;
    }

    public async Task ApplyJambScholarshipsAsync(Guid studentId, Guid sessionId)
    {
        var student = await db.Students
            .Include(s => s.AcademicSession)
            .FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException("Student not found.");
            
        int? score = student.JambScore;

        if (!score.HasValue && student.AdmissionApplicationId.HasValue)
        {
            var app = await db.AdmissionApplications.FindAsync(student.AdmissionApplicationId.Value);
            if (app != null && app.ApplicantType == ApplicantType.DirectEntry)
            {
                var convertedScore = ConvertDirectEntryToJambScore(app.DirectEntryQualification, app.DirectEntryPoints, app.DirectEntryGrade);
                if (convertedScore > 0)
                {
                    score = convertedScore;
                }
            }
        }

        if (!score.HasValue) return;

        var actualScore = score.Value;

        // Find applicable JAMB scholarships based on score tiers
        var jambScholarships = await db.Scholarships
            .Where(s => s.IsActive && s.Type == ScholarshipType.JAMB)
            .ToListAsync();

        var applicable = jambScholarships.Where(s => 
            (!s.MinJambScore.HasValue || actualScore >= s.MinJambScore.Value) &&
            (!s.MaxJambScore.HasValue || actualScore <= s.MaxJambScore.Value)
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
        // Get all students admitted in this session who have a JAMB score or are Direct Entry
        var students = await db.Students
            .Include(s => s.AdmissionApplication)
            .Where(s => s.AcademicSessionId == admissionSessionId && (
                s.JambScore.HasValue ||
                (s.AdmissionApplicationId.HasValue && s.AdmissionApplication.ApplicantType == ApplicantType.DirectEntry)
            ))
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
