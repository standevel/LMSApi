using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class AutoResolveConflictRequest
{
    public Guid ConflictingSlotId { get; set; }
    public Guid ReplacementLecturerId { get; set; }
}

public class AutoResolveConflictEndpoint(ITimetableService timetableService)
    : ApiEndpoint<AutoResolveConflictRequest, LectureTimetableSlot>
{
    public override void Configure()
    {
        Post("timetable/resolve-conflict");
        Policies(PermissionPolicy.Build(LmsPermissions.TimetableManage));
        Description(d => d
            .WithName("AutoResolveConflict")
            .WithTags("Timetable")
            .WithSummary("Endpoint for AutoResolveConflict"));
    }

    public override async Task HandleAsync(AutoResolveConflictRequest req, CancellationToken ct)
    {
        var result = await timetableService.AutoResolveConflictAsync(req.ConflictingSlotId, req.ReplacementLecturerId);
        await SendSuccessAsync(result, ct, "Conflict resolved successfully");
    }
}
