using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public sealed class ScheduleExamRequest
{
    public Guid CourseOfferingId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime ExamDate { get; set; }
    public decimal MaxMarks { get; set; } = 70;
    public double DurationHours { get; set; } = 2.0;
}

public sealed class ScheduleExamEndpoint(ITimetableService timetableService)
    : ApiEndpoint<ScheduleExamRequest, StudentExamDto>
{
    public override void Configure()
    {
        Post("timetable/exams");
        Policies(PermissionPolicy.Build(LmsPermissions.TimetableManage));
        Description(d => d
            .WithName("ScheduleExam")
            .WithTags("Timetable")
            .WithSummary("Schedule a new exam assessment for a course offering"));
    }

    public override async Task HandleAsync(ScheduleExamRequest req, CancellationToken ct)
    {
        if (req.CourseOfferingId == Guid.Empty)
        {
            await SendFailureAsync(400, "Bad Request", "MISSING_OFFERING", "A valid CourseOfferingId is required.", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Title))
        {
            await SendFailureAsync(400, "Bad Request", "MISSING_TITLE", "Title is required.", ct);
            return;
        }

        var result = await timetableService.ScheduleExamAsync(
            req.CourseOfferingId, req.Title, req.Description, req.ExamDate, req.MaxMarks, req.DurationHours, ct);

        if (result.IsError)
        {
            var err = result.FirstError;
            await SendFailureAsync(400, err.Description, err.Code, err.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
