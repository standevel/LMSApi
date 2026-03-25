using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Programs;

public sealed record UpdateCriteriaRequest(Guid ProgramId, int MinJambScore, int MaxAdmissions, string RequiredJambSubjectsJson, string RequiredOLevelSubjectsJson);

public sealed class UpdateProgramCriteriaEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<UpdateCriteriaRequest, ProgramResponse>
{
    public override void Configure()
    {
        Put("/api/programs/criteria/{ProgramId}");
        AllowAnonymous(); // TODO: Restrict to Admin role
    }

    public override async Task HandleAsync(UpdateCriteriaRequest req, CancellationToken ct)
    {
        try
        {
            var program = await admissionService.UpdateProgramCriteriaAsync(
                req.ProgramId, req.MinJambScore, req.MaxAdmissions,
                req.RequiredJambSubjectsJson, req.RequiredOLevelSubjectsJson);

            var response = new ProgramResponse(program.Id, program.Name, program.Code);
            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Program Not Found", "not_found", ex.Message, ct);
        }
    }
}
