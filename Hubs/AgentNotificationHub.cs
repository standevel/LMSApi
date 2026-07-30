using Microsoft.AspNetCore.SignalR;

namespace LMS.Api.Hubs;

public interface IAgentClient
{
    Task ReceiveAgentMessage(string agentPersona, string message, object? cardData);
    Task ReceiveAgentAlert(string alertType, string title, string description);
}

public class AgentNotificationHub : Hub<IAgentClient>
{
    public async Task JoinStudentGroup(string studentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Student_{studentId}");
    }

    public async Task LeaveStudentGroup(string studentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Student_{studentId}");
    }
}
