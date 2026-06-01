using FastEndpoints;

namespace LMS.Api.Endpoints.Fees;

public sealed class FeeGroup : Group
{
    public FeeGroup()
    {
        Configure("fees", ep =>
        {
            ep.Description(x => x
                .WithTags("Fees")
                .WithDescription("Fee templates, categories, payments, and student billing endpoints"));
        });
    }
}
