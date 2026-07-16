using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public sealed class GetAdminExamsRequest
{
    public Guid AcademicSessionId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public sealed class GetAdminExamsEndpoint(ITimetableService timetableService)
    : ApiEndpoint<GetAdminExamsRequest, IEnumerable<StudentExamDto>>
{
    public override void Configure()
    {
        Get("timetable/exams");
        Roles("AcademicAdmin", "Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("GetAdminExams")
            .WithTags("Timetable")
            .WithSummary("Retrieve all exams for a session, optionally filtered by faculty or department"));
    }

    public override async Task HandleAsync(GetAdminExamsRequest req, CancellationToken ct)
    {
        if (req.AcademicSessionId == Guid.Empty)
        {
            await SendFailureAsync(400, "Bad Request", "MISSING_SESSION", "A valid academicSessionId query parameter is required.", ct);
            return;
        }

        var result = await timetableService.GetAdminExamsAsync(req.AcademicSessionId, req.FacultyId, req.DepartmentId, ct);
        await SendSuccessAsync(result, ct);
    }
}
