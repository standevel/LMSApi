using FastEndpoints;

namespace LMS.Api.Endpoints.Reporting;

public class ReportingGroup : Group
{
    public ReportingGroup()
    {
        Configure("reports", ep =>
        {
            ep.Description(x => x
                .WithTags("Reporting")
                .WithDescription("Reporting endpoints"));
        });
    }
}
