using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Fees;

public sealed class AddFeeLineItemEndpoint(IFeeService feeService)
    : ApiEndpoint<AddFeeLineItemRequest, FeeLineItemResponse>
{
    public override void Configure()
    {
        Post("fees/templates/{id}/items");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(AddFeeLineItemRequest req, CancellationToken ct)
    {
        var templateId = Route<Guid>("id");
        try
        {
            var item = await feeService.AddLineItemAsync(templateId, req);
            await SendSuccessAsync(new FeeLineItemResponse(item.Id, item.Name, item.Description, item.Amount, item.IsOptional, item.SortOrder), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Template not found", "NOT_FOUND", "Fee template not found", ct);
        }
    }
}

public sealed class UpdateFeeLineItemEndpoint(IFeeService feeService)
    : ApiEndpoint<UpdateFeeLineItemRequest, FeeLineItemResponse>
{
    public override void Configure()
    {
        Put("fees/items/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(UpdateFeeLineItemRequest req, CancellationToken ct)
    {
        var itemId = Route<Guid>("id");
        try
        {
            var item = await feeService.UpdateLineItemAsync(itemId, req);
            await SendSuccessAsync(new FeeLineItemResponse(item.Id, item.Name, item.Description, item.Amount, item.IsOptional, item.SortOrder), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Item not found", "NOT_FOUND", "Fee line item not found", ct);
        }
    }
}

public sealed class DeleteFeeLineItemEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Delete("fees/items/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var itemId = Route<Guid>("id");
        try
        {
            await feeService.DeleteLineItemAsync(itemId);
            await SendSuccessAsync("Deleted", ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Item not found", "NOT_FOUND", "Fee line item not found", ct);
        }
    }
}
