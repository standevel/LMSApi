using FastEndpoints;

namespace LMS.Api.Endpoints.Admin;

public sealed class AdminGroup : Group
{
    public AdminGroup()
    {
        Configure("", ep =>
        {
            //ep.Roles("Admin");
            ep.Description(x => x
                .WithTags("Administration"));
        });
    }
}
