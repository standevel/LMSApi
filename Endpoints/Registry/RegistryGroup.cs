using FastEndpoints;

namespace LMS.Api.Endpoints.Registry;

public sealed class RegistryGroup : Group
{
    public RegistryGroup()
    {
        Configure("registry", ep =>
        {
            ep.Description(x => x
                .WithTags("Registry")
                .WithDescription("Student registry and institutional statistics endpoints"));
        });
    }
}
