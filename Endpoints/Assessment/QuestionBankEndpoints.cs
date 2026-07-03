using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Assessment;

public sealed class CreateQuestionBankEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<CreateQuestionBankRequest, QuestionBankDto>
{
    public override void Configure()
    {
        Post("question-banks");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateQuestionBankRequest req, CancellationToken ct)
    {
        var result = await questionBankService.CreateQuestionBankAsync(req.Name, req.Description, req.CourseOfferingId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuestionBanksEndpoint(IQuestionBankService questionBankService)
    : ApiEndpointWithoutRequest<List<QuestionBankDto>>
{
    public override void Configure()
    {
        Get("question-banks");
        AllowAnonymous();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courseOfferingIdRaw = Query<string?>("courseOfferingId", isRequired: false);
        var courseOfferingId = Guid.TryParse(courseOfferingIdRaw, out var parsedCourseOfferingId)
            ? parsedCourseOfferingId
            : (Guid?)null;

        var result = await questionBankService.GetQuestionBanksByCourseAsync(courseOfferingId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuestionBankByIdEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<GetQuestionBankByIdEndpoint.GetQuestionBankByIdRequest, QuestionBankDto>
{
    public class GetQuestionBankByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("question-banks/{Id}");
        AllowAnonymous();
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuestionBankByIdRequest req, CancellationToken ct)
    {
        var result = await questionBankService.GetQuestionBankByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuestionBankEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<UpdateQuestionBankEndpoint.UpdateQuestionBankRequest, QuestionBankDto>
{
    public class UpdateQuestionBankRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Name { get; set; } = string.Empty;
        [Microsoft.AspNetCore.Mvc.FromBody] public string Description { get; set; } = string.Empty;
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid? CourseOfferingId { get; set; }
    }

    public override void Configure()
    {
        Put("question-banks/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuestionBankRequest req, CancellationToken ct)
    {
        var result = await questionBankService.UpdateQuestionBankAsync(req.Id, req.Name, req.Description, req.CourseOfferingId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuestionBankEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<DeleteQuestionBankEndpoint.DeleteQuestionBankRequest, Deleted>
{
    public class DeleteQuestionBankRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("question-banks/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuestionBankRequest req, CancellationToken ct)
    {
        var result = await questionBankService.DeleteQuestionBankAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuestionBankItemsEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<GetQuestionBankItemsEndpoint.GetQuestionBankItemsRequest, List<QuestionBankItemDto>>
{
    public class GetQuestionBankItemsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuestionBankId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromQuery] public string? Search { get; set; }
        [Microsoft.AspNetCore.Mvc.FromQuery] public string? Type { get; set; }
        [Microsoft.AspNetCore.Mvc.FromQuery] public string? Difficulty { get; set; }
        [Microsoft.AspNetCore.Mvc.FromQuery] public string? Category { get; set; }
    }

    public override void Configure()
    {
        Get("question-banks/{QuestionBankId}/questions");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuestionBankItemsRequest req, CancellationToken ct)
    {
        var result = await questionBankService.GetQuestionBankItemsAsync(req.QuestionBankId, req.Search, req.Type, req.Difficulty, req.Category, ct);
        await SendAsync(result, ct);
    }
}

public sealed class AddQuestionToQuestionBankEndpoint(IQuestionBankService questionBankService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<AddQuestionToQuestionBankEndpoint.AddQuestionToQuestionBankRequest, QuestionBankItemDto>
{
    public class AddQuestionToQuestionBankRequest : CreateQuestionBankItemRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuestionBankId { get; set; }
    }

    public override void Configure()
    {
        Post("question-banks/{QuestionBankId}/questions");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(AddQuestionToQuestionBankRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await questionBankService.CreateQuestionBankItemAsync(req.QuestionBankId, req, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DownloadQuestionBankImportTemplateEndpoint(IQuestionBankService questionBankService)
    : ApiEndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Get("question-banks/import-template");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await questionBankService.GenerateQuestionImportTemplateAsync(ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        var template = result.Value;
        HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{template.FileName}\"";
        HttpContext.Response.ContentType = template.ContentType;
        await HttpContext.Response.Body.WriteAsync(template.FileContent, ct);
        await HttpContext.Response.CompleteAsync();
    }
}

public sealed class PreviewQuestionBankImportEndpoint(IQuestionBankService questionBankService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<QuestionImportResultDto>
{
    public override void Configure()
    {
        Post("question-banks/{QuestionBankId:guid}/questions/import/preview");
        Roles("SuperAdmin", "Admin", "Lecturer");
        AllowFileUploads();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var questionBankId = Route<Guid>("QuestionBankId");
        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file is null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await questionBankService.ImportQuestionBankItemsAsync(questionBankId, file, userId.Value, previewOnly: true, ct: ct);
        await SendAsync(result, ct);
    }
}

public sealed class ImportQuestionBankQuestionsEndpoint(IQuestionBankService questionBankService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<QuestionImportResultDto>
{
    public override void Configure()
    {
        Post("question-banks/{QuestionBankId:guid}/questions/import");
        Roles("SuperAdmin", "Admin", "Lecturer");
        AllowFileUploads();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var questionBankId = Route<Guid>("QuestionBankId");
        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file is null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await questionBankService.ImportQuestionBankItemsAsync(questionBankId, file, userId.Value, previewOnly: false, ct: ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuestionBankItemEndpoint(IQuestionBankService questionBankService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateQuestionBankItemEndpoint.UpdateQuestionBankItemEndpointRequest, QuestionBankItemDto>
{
    public class UpdateQuestionBankItemEndpointRequest : UpdateQuestionBankItemRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("question-bank-questions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuestionBankItemEndpointRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await questionBankService.UpdateQuestionBankItemAsync(req.Id, req, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DuplicateQuestionBankItemEndpoint(IQuestionBankService questionBankService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<DuplicateQuestionBankItemEndpoint.DuplicateQuestionBankItemRequest, QuestionBankItemDto>
{
    public class DuplicateQuestionBankItemRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Post("question-bank-questions/{Id}/duplicate");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DuplicateQuestionBankItemRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await questionBankService.DuplicateQuestionBankItemAsync(req.Id, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuestionBankItemEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<DeleteQuestionBankItemEndpoint.DeleteQuestionBankItemRequest, Deleted>
{
    public class DeleteQuestionBankItemRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("question-bank-questions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuestionBankItemRequest req, CancellationToken ct)
    {
        var result = await questionBankService.DeleteQuestionBankItemAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}
