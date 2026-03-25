using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Registry;

public sealed class GetRegistryStatsEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<EmptyRequest, RegistryStatsResponse>
{
    public override void Configure()
    {
        Get("/api/registry/stats");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var totalStudents = await dbContext.Enrollments.CountAsync(ct);
        
        var undergraduateStudents = await dbContext.Enrollments
            .Include(e => e.Program)
            .CountAsync(e => e.Program.Type == ProgramType.Undergraduate, ct);
            
        var postgraduateStudents = await dbContext.Enrollments
            .Include(e => e.Program)
            .CountAsync(e => e.Program.Type == ProgramType.Postgraduate, ct);
        
        var newAdmissions = await dbContext.AdmissionApplications
            .CountAsync(a => a.Status == AdmissionStatus.Submitted, ct);
            
        var pendingDocuments = await dbContext.DocumentRecords
            .CountAsync(d => d.Status == DocumentStatus.Pending, ct);

        await SendSuccessAsync(new RegistryStatsResponse(
            totalStudents,
            undergraduateStudents,
            postgraduateStudents,
            newAdmissions,
            pendingDocuments
        ), ct);
    }
}
