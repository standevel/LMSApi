using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services.AI;
using LMS.Api.Services.AI.Models;

namespace LMS.Api.Endpoints.Agent;

public class AgentChatEndpoint : ApiEndpoint<AgentChatRequest, AgentChatResponse>
{
    private readonly IAgentOrchestratorService _orchestratorService;

    public AgentChatEndpoint(IAgentOrchestratorService orchestratorService)
    {
        _orchestratorService = orchestratorService;
    }

    public override void Configure()
    {
        Post("ai/chat");
        AllowAnonymous(); // Permissive for testing, can be secured via JWT claims
        Summary(s =>
        {
            s.Summary = "Sends a prompt to the AI Agent Orchestrator";
            s.Description = "Executes multi-agent tool calling and returns text along with optional Generative UI cards.";
        });
    }

    public override async Task HandleAsync(AgentChatRequest req, CancellationToken ct)
    {
        var result = await _orchestratorService.ProcessChatAsync(req, ct);
        await SendSuccessAsync(result, ct, "Agent processed request successfully");
    }
}
