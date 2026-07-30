using LMS.Api.Data;
using LMS.Api.Data.Enums;
using LMS.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI;

public class AIAgentInterventionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<AgentNotificationHub, IAgentClient> _hubContext;
    private readonly ILogger<AIAgentInterventionBackgroundService> _logger;

    public AIAgentInterventionBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<AgentNotificationHub, IAgentClient> hubContext,
        ILogger<AIAgentInterventionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Agent Intervention Background Service started.");

        // Periodic monitoring loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformInterventionCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AI Agent intervention check execution.");
            }

            // Run check every 15 minutes
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    public async Task PerformInterventionCheckAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LmsDbContext>();

        _logger.LogInformation("Executing proactive AI Agent intervention scans...");

        // 1. Scan for Outstanding Fee Balances
        var outstandingBills = await dbContext.StudentFeeRecords
            .Where(f => f.Status == FeeRecordStatus.Outstanding || f.Status == FeeRecordStatus.PartiallyPaid)
            .OrderByDescending(f => f.GeneratedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var bill in outstandingBills)
        {
            await _hubContext.Clients.Group($"Student_{bill.StudentId}").ReceiveAgentAlert(
                alertType: "fee_clearance",
                title: "💳 Financial Clearance Alert",
                description: $"You have an outstanding fee balance of {bill.Balance:C}. Click to speak with your AI Bursar Assistant about payment clearance."
            );
        }

        _logger.LogInformation("Completed AI Agent intervention scan. Inspected {FeeRecords} fee records.", outstandingBills.Count);
    }
}
