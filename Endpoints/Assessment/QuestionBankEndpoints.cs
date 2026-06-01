using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
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
        var courseOfferingId = QueryParam<Guid>("courseOfferingId");
        var result = await questionBankService.GetQuestionBanksByCourseAsync(courseOfferingId ?? Guid.Empty, ct);
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

public sealed class AddQuestionToQuestionBankEndpoint(IQuestionBankService questionBankService)
    : ApiEndpoint<AddQuestionToQuestionBankEndpoint.AddQuestionToQuestionBankRequest, bool>
{
    public class AddQuestionToQuestionBankRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuestionBankId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string QuestionText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public int OrderIndex { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string QuestionType { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public List<string> OptionTexts { get; set; } = new();
    }

    public override void Configure()
    {
        Post("question-banks/{QuestionBankId}/questions");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(AddQuestionToQuestionBankRequest req, CancellationToken ct)
    {
        // Note: The QuestionBankService doesn't currently have an AddQuestionToQuestionBank method
        // This would need to be implemented in the service
        await SendFailureAsync(501, "Not Implemented", "NOT_IMPLEMENTED", "Add question to question bank functionality not yet implemented", ct);
    }
}
