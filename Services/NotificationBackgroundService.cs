using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromHours(1); // Poll every hour

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUpcomingAssessmentsAsync(stoppingToken);
                await ProcessUpcomingLecturesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing notification background task.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("NotificationBackgroundService is stopping.");
    }

    private async Task ProcessUpcomingAssessmentsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTimeOffset.UtcNow;
        var upcomingThreshold = now.AddDays(1); // Notify 24 hours before due date

        // Find assignments due in the next 24 hours that haven't sent a reminder
        var upcomingAssignments = await dbContext.Assignments
            .Include(a => a.CourseOffering)
            .Where(a => !a.IsDeleted && !a.ReminderSent && a.DueDate > now && a.DueDate <= upcomingThreshold)
            .ToListAsync(ct);

        foreach (var assignment in upcomingAssignments)
        {
            var enrolledStudentIds = await dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.CourseOfferingId == assignment.CourseOfferingId && e.Status == "Registered")
                .Select(e => e.StudentId)
                .ToListAsync(ct);

            foreach (var studentId in enrolledStudentIds)
            {
                await notificationService.CreateAsync(new CreateNotificationRequest(
                    studentId,
                    null, // System
                    $"Upcoming Assignment: {assignment.Title}",
                    $"Reminder: Assignment '{assignment.Title}' is due on {assignment.DueDate:f}.",
                    "System",
                    $"/dashboard/student/courses/{assignment.CourseOfferingId}"
                ), ct);
            }

            assignment.ReminderSent = true;
        }

        if (upcomingAssignments.Any())
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation($"Sent reminders for {upcomingAssignments.Count} upcoming assignments.");
        }
    }

    private async Task ProcessUpcomingLecturesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var timeThreshold = TimeOnly.FromDateTime(now.AddHours(1));

        // Find lectures scheduled for today within the next hour
        var upcomingLectures = await dbContext.LectureSessions
            .Include(l => l.CourseOffering)
            .ThenInclude(co => co.Course)
            .Where(l => !l.ReminderSent && !l.IsCompleted && l.SessionDate == today && l.StartTime > TimeOnly.FromDateTime(now) && l.StartTime <= timeThreshold)
            .ToListAsync(ct);

        foreach (var lecture in upcomingLectures)
        {
            var enrolledStudentIds = await dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.CourseOfferingId == lecture.CourseOfferingId && e.Status == "Registered")
                .Select(e => e.StudentId)
                .ToListAsync(ct);

            foreach (var studentId in enrolledStudentIds)
            {
                await notificationService.CreateAsync(new CreateNotificationRequest(
                    studentId,
                    lecture.CreatedBy, 
                    $"Upcoming Lecture",
                    $"Reminder: You have a lecture for {lecture.CourseOffering.Course.Code} starting at {lecture.StartTime:t}.",
                    "System",
                    $"/dashboard/student/courses/{lecture.CourseOfferingId}"
                ), ct);
            }

            lecture.ReminderSent = true;
        }

        if (upcomingLectures.Any())
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation($"Sent reminders for {upcomingLectures.Count} upcoming lectures.");
        }
    }
}
