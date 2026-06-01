using FastEndpoints;

namespace LMS.Api.Endpoints.Health;

public sealed class HealthGroup : Group
{
    public HealthGroup()
    {
        Configure("health", ep =>
        {
            ep.Description(x => x
                .WithTags("Health")
                .WithDescription("API health check and system status endpoints"));
        });
    }
}
