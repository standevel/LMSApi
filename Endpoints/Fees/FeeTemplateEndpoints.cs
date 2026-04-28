using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Fees;

public sealed class CreateFeeTemplateEndpoint(IFeeService feeService)
    : ApiEndpoint<CreateFeeTemplateRequest, FeeTemplateResponse>
{
    public override void Configure()
    {
        Post("/api/fees/templates");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CreateFeeTemplateRequest req, CancellationToken ct)
    {
        var template = await feeService.CreateTemplateAsync(req);
        var full = await feeService.GetTemplateByIdAsync(template.Id);
        await SendSuccessAsync(FeeMapper.ToResponse(full!), ct);
    }
}

public sealed class UpdateFeeTemplateEndpoint(IFeeService feeService)
    : ApiEndpoint<UpdateFeeTemplateRequest, FeeTemplateResponse>
{
    public override void Configure()
    {
        Put("/api/fees/templates/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(UpdateFeeTemplateRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var template = await feeService.UpdateTemplateAsync(id, req);
            var full = await feeService.GetTemplateByIdAsync(template.Id);
            await SendSuccessAsync(FeeMapper.ToResponse(full!), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Template not found", "NOT_FOUND", "Fee template not found", ct);
        }
    }
}

public sealed class ToggleFeeTemplateEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<FeeTemplateResponse>
{
    public override void Configure()
    {
        Patch("/api/fees/templates/{id}/toggle");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var template = await feeService.ToggleTemplateAsync(id);
            var full = await feeService.GetTemplateByIdAsync(template.Id);
            await SendSuccessAsync(FeeMapper.ToResponse(full!), ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Template not found", "NOT_FOUND", "Fee template not found", ct);
        }
    }
}

public sealed class GetFeeTemplatesEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeeTemplateResponse>>
{
    public override void Configure()
    {
        Get("/api/fees/templates");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var activeOnlyStr = Query<string?>("activeOnly", isRequired: false);
        bool? activeOnly = activeOnlyStr is null ? null : bool.TryParse(activeOnlyStr, out var b) ? b : null;
        var templates = await feeService.GetTemplatesAsync(activeOnly);
        await SendSuccessAsync(templates.Select(FeeMapper.ToResponse), ct);
    }
}

public sealed class GetFeeTemplateByIdEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<FeeTemplateResponse>
{
    public override void Configure()
    {
        Get("/api/fees/templates/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var template = await feeService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            await SendFailureAsync(404, "Template not found", "NOT_FOUND", "Fee template not found", ct);
            return;
        }
        await SendSuccessAsync(FeeMapper.ToResponse(template), ct);
    }
}
