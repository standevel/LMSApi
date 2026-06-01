using FastEndpoints;

namespace LMS.Api.Endpoints.Courses;

public sealed class CourseGroup : Group
{
    public CourseGroup()
    {
        Configure("courses", ep =>
        {
            ep.Description(x => x
                .WithTags("Courses")
                .WithDescription("Course catalog, materials, and offerings management endpoints"));
        });
    }
}
