using FastEndpoints;

namespace LMS.Api.Endpoints.Admissions;

public sealed class AdmissionGroup : Group
{
    public AdmissionGroup()
    {
        Configure("admissions", ep =>
        {
            ep.Description(x => x
                .WithTags("Admissions")
                .WithDescription("Student admission application and document management endpoints"));
        });
    }
}
