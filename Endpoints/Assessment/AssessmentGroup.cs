using FastEndpoints;

namespace LMS.Api.Endpoints.Assessment;

public class AssessmentGroup : Group
{
    public AssessmentGroup()
    {
        Configure("assessment", ep =>
        {
            ep.AllowAnonymous();
        });
    }
}