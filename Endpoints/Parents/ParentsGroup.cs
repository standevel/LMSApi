using FastEndpoints;

namespace LMS.Api.Endpoints.Parents;

public class ParentsGroup : Group
{
    public ParentsGroup()
    {
        Configure("parents", ep =>
        {
            // No specific configuration needed for now
        });
    }
}