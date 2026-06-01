using FastEndpoints;

namespace LMS.Api.Endpoints.Auth;

public sealed class AuthGroup : Group
{
    public AuthGroup()
    {
        Configure("auth", ep =>
        {
            ep.Description(x => x
                .WithTags("Authentication")
                .WithDescription("Authentication and user session management endpoints"));
        });
    }
}
