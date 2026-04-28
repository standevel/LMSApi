using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

internal static class AdmissionResponseMapper
{
    internal static async Task<(Guid? StudentUserId, StudentFeeRecord? FeeRecord)> GetOfferFeeContextAsync(
        LmsDbContext dbContext,
        AdmissionApplication app,
        CancellationToken ct)
    {
        var student = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email == app.StudentEmail || x.EntraObjectId == $"admission:{app.Id}",
                ct);

        if (student is null)
        {
            return (null, null);
        }

        var feeRecord = await dbContext.StudentFeeRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == student.Id && x.SessionId == app.AcademicSessionId, ct);

        return (student.Id, feeRecord);
    }

    internal static AdmissionApplicationResponse Map(
        AdmissionApplication app,
        Guid? studentUserId = null,
        StudentFeeRecord? feeRecord = null)
        => new(
            app.Id,
            app.ApplicationNumber,
            app.FirstName,
            app.LastName,
            app.MiddleName,
            app.StudentEmail,
            app.JambRegNumber,
            app.AcademicSessionId,
            app.AcademicSession?.Name ?? string.Empty,
            app.Persona,
            app.FacultyId,
            app.Faculty?.Name ?? string.Empty,
            app.AcademicProgramId,
            app.AcademicProgram?.Name ?? string.Empty,
            app.ProgramReason,
            app.QualificationsJson,
            app.Phone,
            app.EmergencyContactJson,
            app.SponsorshipJson,
            app.Status.ToString(),
            app.CreatedAt,
            app.SubmittedAt,
            app.Documents.Select(d => new DocumentResponse(
                d.Id,
                d.FileName,
                d.FileUrl,
                d.DocumentTypeId,
                d.DocumentType?.Name ?? "Admission Document",
                d.DocumentType?.Code ?? string.Empty,
                d.Status.ToString(),
                d.RejectionReason
            )),
            studentUserId,
            feeRecord?.Id,
            feeRecord?.TotalAmount,
            feeRecord?.Balance,
            feeRecord?.Status.ToString(),
            feeRecord is not null && feeRecord.TotalAmount > 0
        );
}
