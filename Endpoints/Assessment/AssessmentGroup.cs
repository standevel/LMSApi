using FastEndpoints;

namespace LMS.Api.Endpoints.Assessment;

public class AssessmentGroup : Group
{
    public AssessmentGroup()
    {
        Configure("assessment", ep =>
        {
            // Endpoints in this group require authentication by default
        });
    }
}