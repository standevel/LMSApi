using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IAdmissionService
{
    Task<AdmissionApplication?> VerifyIdentityAsync(string email, string jambRegNumber);
    Task<AdmissionApplication> SaveApplicationAsync(AdmissionApplication application, IEnumerable<Guid>? documentIds = null);
    Task<AdmissionApplication> SubmitApplicationAsync(Guid applicationId);
    Task<IEnumerable<AdmissionApplication>> GetHistoryByEmailAsync(string email);
    Task<IEnumerable<AdmissionApplication>> GetHistoryByJambAsync(string jambRegNumber);
    Task<IEnumerable<Faculty>> GetFacultiesAsync();
    Task<IEnumerable<AcademicProgram>> GetProgramsByFacultyAsync(Guid facultyId);
    Task<IEnumerable<AcademicSession>> GetAdmissionSessionsAsync();
    Task<IEnumerable<SponsorOrganization>> GetAdmissionSponsorsAsync();
    Task<IEnumerable<Subject>> GetAdmissionSubjectsAsync();

    // Admin Methods
    Task<AdmissionApplication?> GetApplicationByIdAsync(Guid id);
    Task<IEnumerable<AdmissionApplication>> GetApplicationsAsync(AdmissionStatus? status = null, Guid? sessionId = null);
    Task<AdmissionApplication> UpdateApplicationStatusAsync(Guid id, AdmissionStatus status);
    Task<IEnumerable<AutoAdmitResult>> AutoAdmitAsync(Guid sessionId, bool isDryRun);
    Task<AcademicProgram> UpdateProgramCriteriaAsync(Guid programId, int minScore, int maxAdmissions, string jambSubjectsJson, string oLevelSubjectsJson);
}

public record AutoAdmitResult(Guid ApplicationId, string StudentName, string ProgramName, int JambScore, bool IsAdmitted, string? Reason);
