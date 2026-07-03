using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admin;

public sealed record AdminDashboardStatsResponse(
    int TotalUsers,
    decimal TotalUsersGrowthPercentage,
    int ActiveSessions,
    decimal SystemHealth,
    int PendingRequests,
    List<AuditLogDto> RecentLogs);

public sealed class GetAdminDashboardStatsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<AdminDashboardStatsResponse>
{
    public override void Configure()
    {
        Get("admin/dashboard-stats");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Get administrative dashboard statistics";
            s.Description = "Retrieves total users, growth, active sessions, system health, pending requests, and recent audit logs.";
            s.Responses[200] = "Successfully retrieved statistics.";
            s.Responses[401] = "Unauthorized access.";
            s.Responses[403] = "Forbidden access.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 1. Total Users
        var totalUsers = await dbContext.Users.CountAsync(ct);

        // 2. Total Users Growth Percentage (last 30 days vs prior 30 days)
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        var usersLast30Days = await dbContext.Users.CountAsync(u => u.CreatedUtc >= thirtyDaysAgo, ct);
        var usersPrior30Days = await dbContext.Users.CountAsync(u => u.CreatedUtc >= sixtyDaysAgo && u.CreatedUtc < thirtyDaysAgo, ct);

        decimal growthPercentage = 0;
        if (usersPrior30Days > 0)
        {
            growthPercentage = Math.Round(((decimal)(usersLast30Days - usersPrior30Days) / usersPrior30Days) * 100, 1);
        }
        else if (usersLast30Days > 0)
        {
            growthPercentage = 100.0m;
        }

        // 3. Active Sessions (unique user activity in last 30 minutes based on AuditLogs)
        var activeSessions = await dbContext.AuditLogs
            .Where(log => log.Timestamp >= now.AddMinutes(-30))
            .Where(log => log.UserId.HasValue)
            .Select(log => log.UserId)
            .Distinct()
            .CountAsync(ct);
        
        if (activeSessions == 0)
        {
            activeSessions = 1; // Fallback to 1 representing the current logged in user
        }

        // 4. System Health
        var canConnect = await dbContext.Database.CanConnectAsync(ct);
        decimal systemHealth = canConnect ? 100.0m : 0.0m;

        // 5. Pending Requests
        var pendingAdmissionApps = await dbContext.AdmissionApplications.CountAsync(a => a.Status == AdmissionStatus.Submitted, ct);
        var pendingTranscriptRequests = await dbContext.TranscriptRequests.CountAsync(t => t.Status == TranscriptStatus.Pending, ct);
        var pendingSwitchRequests = await dbContext.ProgramSwitchRequests.CountAsync(r => r.Status == ProgramSwitchStatus.PendingAdminAction, ct);
        var pendingOverrides = await dbContext.PrerequisiteOverrides.CountAsync(o => o.Status == "Pending", ct);

        var pendingRequests = pendingAdmissionApps + pendingTranscriptRequests + pendingSwitchRequests + pendingOverrides;

        // 6. Recent logs (last 5 logs)
        var recentLogsList = await dbContext.AuditLogs
            .Include(log => log.User)
            .OrderByDescending(log => log.Timestamp)
            .Take(5)
            .ToListAsync(ct);

        var recentLogs = recentLogsList.Select(x => new AuditLogDto(
            x.Id,
            x.Action,
            x.EntityName,
            x.EntityId,
            x.Changes,
            x.UserId,
            x.User != null ? (x.User.DisplayName ?? x.User.Email) : "System",
            x.Timestamp
        )).ToList();

        var data = new AdminDashboardStatsResponse(
            totalUsers,
            growthPercentage,
            activeSessions,
            systemHealth,
            pendingRequests,
            recentLogs
        );

        await SendSuccessAsync(data, ct);
    }
}
