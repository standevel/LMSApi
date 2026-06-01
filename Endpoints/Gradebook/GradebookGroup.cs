using FastEndpoints;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GradebookGroup : Group
{
    public GradebookGroup()
    {
        Configure("gradebook", ep =>
        {
            ep.Description(x => x
                .WithTags("Gradebook")
                .WithDescription("Grade entry, assessment, grading configurations, and grade publication endpoints"));
        });
    }
}
