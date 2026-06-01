using FastEndpoints;

namespace LMS.Api.Endpoints.Timetable;

public sealed class TimetableGroup : Group
{
    public TimetableGroup()
    {
        Configure("timetable", ep =>
        {
            ep.Description(x => x
                .WithTags("Timetable")
                .WithDescription("Lecture timetable scheduling, conflict detection, and resolution endpoints"));
        });
    }
}
