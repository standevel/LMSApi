using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Fees;

public sealed class CreateFeeCategoryEndpoint(IFeeService feeService)
    : ApiEndpoint<CreateFeeCategoryRequest, FeeCategoryResponse>
{
    public override void Configure()
    {
        Post("fees/categories");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(CreateFeeCategoryRequest req, CancellationToken ct)
    {
        var category = await feeService.CreateCategoryAsync(req);
        await SendSuccessAsync(FeeMapper.ToResponse(category), ct);
    }
}

public sealed class UpdateFeeCategoryEndpoint(IFeeService feeService)
    : ApiEndpoint<UpdateFeeCategoryRequest, FeeCategoryResponse>
{
    public override void Configure()
    {
        Put("fees/categories/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(UpdateFeeCategoryRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var category = await feeService.UpdateCategoryAsync(id, req);
            await SendSuccessAsync(FeeMapper.ToResponse(category), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Category not found", "NOT_FOUND", "Fee category not found", ct);
        }
    }
}

public sealed class ToggleFeeCategoryEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<FeeCategoryResponse>
{
    public override void Configure()
    {
        Patch("fees/categories/{id}/toggle");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var category = await feeService.ToggleCategoryAsync(id);
            await SendSuccessAsync(FeeMapper.ToResponse(category), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Category not found", "NOT_FOUND", "Fee category not found", ct);
        }
    }
}

public sealed class GetFeeCategoriesEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeeCategoryResponse>>
{
    public override void Configure()
    {
        Get("fees/categories");
        // Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var activeOnlyStr = Query<string?>("activeOnly", isRequired: false);
        bool? activeOnly = activeOnlyStr is null ? null : bool.TryParse(activeOnlyStr, out var b) ? b : null;
        Console.WriteLine("ActiveOnly: "+activeOnlyStr+" -> "+activeOnly);
        var categories = await feeService.GetCategoriesAsync(activeOnly);
        await SendSuccessAsync(categories.Select(FeeMapper.ToResponse), ct);
    }
}
