using FastEndpoints;

namespace LMS.Api.Endpoints.SelfService;

public class SelfServiceGroup : Group
{
    public SelfServiceGroup()
    {
        Configure("self-service", ep =>
        {
            // No specific configuration needed for now
        });
    }
}