using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.AI;
using LMS.Api.Services.AI.Models;

namespace LMS.Api.Endpoints.Agent;

/// <summary>
/// Admin-only AI assistant endpoint.
/// Requires the 'access.manage' permission — accessible only to Admins and Super Admins.
/// </summary>
public class AdminAgentChatEndpoint : ApiEndpoint<AgentChatRequest, AgentChatResponse>
{
    private readonly IAgentOrchestratorService _orchestratorService;

    public AdminAgentChatEndpoint(IAgentOrchestratorService orchestratorService)
    {
        _orchestratorService = orchestratorService;
    }

    public override void Configure()
    {
        Post("ai/admin/chat");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("AI", "Administration");
        Summary(s =>
        {
            s.Summary = "Admin AI Assistant — System-wide insights";
            s.Description =
                "Sends a prompt to the Admin AI Assistant persona. Requires 'access.manage' permission. " +
                "Provides system overview, user statistics, fee collection summaries, enrollment data, and audit logs.";
        });
    }

    public override async Task HandleAsync(AgentChatRequest req, CancellationToken ct)
    {
        // Force the persona to AdminAssistant regardless of what the caller sends.
        // This prevents callers from using this secured endpoint for other personas.
        req.Persona = AgentPersona.AdminAssistant;

        var result = await _orchestratorService.ProcessChatAsync(req, ct);
        await SendSuccessAsync(result, ct, "Admin assistant processed request successfully");
    }
}
