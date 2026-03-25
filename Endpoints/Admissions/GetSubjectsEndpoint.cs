using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetSubjectsEndpoint(IAdmissionService admissionService) : ApiEndpointWithoutRequest<IEnumerable<SubjectResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/subjects");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var subjects = await admissionService.GetAdmissionSubjectsAsync();
        var response = subjects.Select(s => new SubjectResponse(s.Id, s.Name));
        await SendSuccessAsync(response, ct);
    }
}
