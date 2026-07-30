using LMS.Api.Services.AI.Models;

namespace LMS.Api.Services.AI;

public interface IAgentOrchestratorService
{
    Task<AgentChatResponse> ProcessChatAsync(AgentChatRequest request, CancellationToken ct = default);
}
