using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Communication;

public sealed class CreateDiscussionThreadEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<CreateDiscussionRequest, DiscussionThreadDto>
{
    public override void Configure()
    {
        Post("discussions");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateDiscussionRequest req, CancellationToken ct)
    {
        var result = await discussionService.CreateThreadAsync(req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetDiscussionThreadsEndpoint(IDiscussionService discussionService)
    : ApiEndpointWithoutRequest<List<DiscussionThreadDto>>
{
    public override void Configure()
    {
        Get("discussions");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await discussionService.GetThreadsAsync(ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetDiscussionThreadByIdEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<GetDiscussionThreadByIdEndpoint.GetDiscussionThreadByIdRequest, DiscussionThreadDto>
{
    public class GetDiscussionThreadByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("discussions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(GetDiscussionThreadByIdRequest req, CancellationToken ct)
    {
        var result = await discussionService.GetThreadByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateDiscussionThreadEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<UpdateDiscussionThreadEndpoint.UpdateDiscussionThreadRequest, DiscussionThreadDto>
{
    public class UpdateDiscussionThreadRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Title { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public bool IsPinned { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public bool IsLocked { get; set; }
    }

    public override void Configure()
    {
        Put("discussions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(UpdateDiscussionThreadRequest req, CancellationToken ct)
    {
        var result = await discussionService.UpdateThreadAsync(req.Id, new UpdateDiscussionRequest(req.Title, req.IsPinned, req.IsLocked), ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteDiscussionThreadEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<DeleteDiscussionThreadEndpoint.DeleteDiscussionThreadRequest, bool>
{
    public class DeleteDiscussionThreadRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("discussions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteDiscussionThreadRequest req, CancellationToken ct)
    {
        var result = await discussionService.DeleteThreadAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}

public sealed class CreateDiscussionPostEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<CreateDiscussionPostEndpoint.CreateDiscussionPostRequest, DiscussionPostDto>
{
    public class CreateDiscussionPostRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid ThreadId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid AuthorId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Content { get; set; } = default!;
    }

    public override void Configure()
    {
        Post("discussions/{ThreadId}/posts");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateDiscussionPostRequest req, CancellationToken ct)
    {
        var result = await discussionService.CreatePostAsync(req.ThreadId, req.AuthorId, req.Content, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateDiscussionPostEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<UpdateDiscussionPostEndpoint.UpdateDiscussionPostRequest, DiscussionPostDto>
{
    public class UpdateDiscussionPostRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Content { get; set; } = default!;
    }

    public override void Configure()
    {
        Put("discussions/posts/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Communication");
    }

    public override async Task HandleAsync(UpdateDiscussionPostRequest req, CancellationToken ct)
    {
        var result = await discussionService.UpdatePostAsync(req.Id, req.Content, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteDiscussionPostEndpoint(IDiscussionService discussionService)
    : ApiEndpoint<DeleteDiscussionPostEndpoint.DeleteDiscussionPostRequest, bool>
{
    public class DeleteDiscussionPostRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("discussions/posts/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteDiscussionPostRequest req, CancellationToken ct)
    {
        var result = await discussionService.DeletePostAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}
