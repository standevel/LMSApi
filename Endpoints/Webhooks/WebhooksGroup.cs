using FastEndpoints;

namespace LMS.Api.Endpoints.Webhooks;

public class WebhooksGroup : Group
{
    public WebhooksGroup()
    {
        Configure("webhooks", ep =>
        {
            // No specific configuration needed for now
        });
    }
}