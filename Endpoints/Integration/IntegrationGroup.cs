using FastEndpoints;

namespace LMS.Api.Endpoints.Integration;

public class IntegrationGroup : Group
{
    public IntegrationGroup()
    {
        Configure("integrations", ep =>
        {
            // No specific configuration needed for now
        });
    }
}