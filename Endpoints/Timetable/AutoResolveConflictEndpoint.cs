using FastEndpoints;
using LMS.Api.Data.Entities;
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
        Post("/api/timetable/resolve-conflict");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(AutoResolveConflictRequest req, CancellationToken ct)
    {
        var result = await timetableService.AutoResolveConflictAsync(req.ConflictingSlotId, req.ReplacementLecturerId);
        await SendSuccessAsync(result, ct, "Conflict resolved successfully");
    }
}
