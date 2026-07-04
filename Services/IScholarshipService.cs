using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IScholarshipService
{
    Task<ScholarshipDto> CreateScholarshipAsync(CreateScholarshipRequest req);
    Task<ScholarshipDto> UpdateScholarshipAsync(Guid id, UpdateScholarshipRequest req);
    Task<IEnumerable<ScholarshipDto>> GetAllScholarshipsAsync(bool? activeOnly = null);
    Task<ScholarshipDto?> GetScholarshipByIdAsync(Guid id);
    
    Task<StudentScholarshipDto> AssignScholarshipAsync(AssignScholarshipRequest req);
    Task RemoveScholarshipAssignmentAsync(Guid id);
    Task<IEnumerable<StudentScholarshipDto>> GetStudentScholarshipsAsync(Guid studentId, Guid? sessionId = null);
    Task<IEnumerable<StudentScholarshipDto>> GetAllStudentScholarshipsAsync(int limit = 100);
    
    // Auto-evaluates and applies JAMB scholarships for a student based on their score
    Task ApplyJambScholarshipsAsync(Guid studentId, Guid sessionId);
    
    // Batch evaluates and applies JAMB scholarships for all students admitted in a specific session
    Task ApplyJambScholarshipsForAdmissionSessionAsync(Guid admissionSessionId);
}
