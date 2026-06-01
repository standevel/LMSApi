using FastEndpoints;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class LectureSessionGroup : Group
{
    public LectureSessionGroup()
    {
        Configure("lecture-sessions", ep =>
        {
            ep.Description(x => x
                .WithTags("Lecture Sessions")
                .WithDescription("Lecture session management, attendance, materials, and external links endpoints"));
        });
    }
}
