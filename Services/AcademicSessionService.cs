using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Trigger watch rebuild
namespace LMS.Api.Services;

public sealed class AcademicSessionService(
    IAcademicSessionRepository sessionRepository,
    IAuditService auditService,
    IServiceProvider serviceProvider) : BaseService(auditService), IAcademicSessionService
{
    public async Task<ErrorOr<AcademicSessionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        return session.ToDto();
    }

    public async Task<ErrorOr<List<AcademicSessionDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var sessions = await sessionRepository.GetAllAsync(ct);
        return sessions.OrderByDescending(s => s.StartDate).Select(s => s.ToDto()).ToList();
    }

    public async Task<ErrorOr<AcademicSessionDto>> CreateAsync(CreateAcademicSessionRequest request, CancellationToken ct = default)
    {
        var session = new AcademicSession
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false,
            IsAdmissionActive = false,
            IsAdmissionOpen = request.IsAdmissionOpen,
            ActiveSemester = request.ActiveSemester,
            IsRegistrationOpen = request.IsRegistrationOpen,
            RegistrationStartDate = request.RegistrationStartDate,
            RegistrationEndDate = request.RegistrationEndDate
        };

        await sessionRepository.AddAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "AcademicSession", session.Id.ToString(), $"Created session: {session.Name}", ct);

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> UpdateAsync(Guid id, UpdateAcademicSessionRequest request, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        if (request.IsActive && !session.IsActive)
        {
            var active = await sessionRepository.GetActiveAsync(ct);
            if (active != null && active.Id != session.Id)
            {
                active.IsActive = false;
                await sessionRepository.UpdateAsync(active, ct);
            }
        }

        if (request.IsAdmissionActive && !session.IsAdmissionActive)
        {
            var activeAdmission = await sessionRepository.GetActiveAdmissionAsync(ct);
            if (activeAdmission != null && activeAdmission.Id != session.Id)
            {
                activeAdmission.IsAdmissionActive = false;
                await sessionRepository.UpdateAsync(activeAdmission, ct);
            }
        }

        session.Name = request.Name;
        session.StartDate = request.StartDate;
        session.EndDate = request.EndDate;
        session.ActiveSemester = request.ActiveSemester;
        session.IsActive = request.IsActive;
        session.IsAdmissionActive = request.IsAdmissionActive;
        session.IsAdmissionOpen = request.IsAdmissionOpen;
        session.IsRegistrationOpen = request.IsRegistrationOpen;
        session.RegistrationStartDate = request.RegistrationStartDate;
        session.RegistrationEndDate = request.RegistrationEndDate;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("Update", "AcademicSession", id.ToString(), $"Updated session: {session.Name}", ct);

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        if (!session.IsActive)
        {
            // Deactivate current active session if any
            var active = await sessionRepository.GetActiveAsync(ct);
            if (active != null)
            {
                active.IsActive = false;
                await sessionRepository.UpdateAsync(active, ct);
            }
        }

        session.IsActive = !session.IsActive;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleStatus", "AcademicSession", id.ToString(), $"Session {session.Name} {(session.IsActive ? "activated" : "deactivated")}", ct);

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> ToggleAdmissionStatusAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        if (!session.IsAdmissionActive)
        {
            // Deactivate current active admission session if any
            var active = await sessionRepository.GetActiveAdmissionAsync(ct);
            if (active != null)
            {
                active.IsAdmissionActive = false;
                await sessionRepository.UpdateAsync(active, ct);
            }
        }

        session.IsAdmissionActive = !session.IsAdmissionActive;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleAdmissionStatus", "AcademicSession", id.ToString(), $"Admission Session {session.Name} {(session.IsAdmissionActive ? "activated" : "deactivated")}", ct);

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> ToggleAdmissionOpenStatusAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        session.IsAdmissionOpen = !session.IsAdmissionOpen;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleAdmissionOpenStatus", "AcademicSession", id.ToString(), $"Session {session.Name} admission {(session.IsAdmissionOpen ? "opened" : "closed")}", ct);

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> ToggleRegistrationOpenStatusAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        session.IsRegistrationOpen = !session.IsRegistrationOpen;
        if (session.IsRegistrationOpen && !session.RegistrationStartDate.HasValue)
        {
            session.RegistrationStartDate = DateTime.UtcNow;
        }
        else if (!session.IsRegistrationOpen && !session.RegistrationEndDate.HasValue)
        {
            session.RegistrationEndDate = DateTime.UtcNow;
        }

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleRegistrationOpenStatus", "AcademicSession", id.ToString(),
            $"Course registration for session {session.Name} {(session.IsRegistrationOpen ? "opened" : "closed")}", ct);

        if (session.IsRegistrationOpen)
        {
            await TriggerAutoRegistrationIfEnabledAsync(session.Id, ct);
        }

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> StartRegistrationAsync(Guid id, StartRegistrationRequest? request = null, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        session.IsRegistrationOpen = true;
        session.RegistrationStartDate = request?.StartDate ?? DateTime.UtcNow;
        session.RegistrationEndDate = request?.EndDate;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("StartRegistration", "AcademicSession", id.ToString(),
            $"Started course registration for session {session.Name} (Ends: {session.RegistrationEndDate})", ct);

        if (request?.RunAutoRegistration == true)
        {
            using var scope = serviceProvider.CreateScope();
            var autoRegService = scope.ServiceProvider.GetService<IAutoRegistrationService>();
            if (autoRegService != null)
            {
                await autoRegService.RunBatchAutoRegistrationAsync(new BatchAutoRegistrationRequest
                {
                    AcademicSessionId = session.Id,
                    DryRun = false
                }, ct);
            }
        }
        else
        {
            await TriggerAutoRegistrationIfEnabledAsync(session.Id, ct);
        }

        return session.ToDto();
    }

    public async Task<ErrorOr<AcademicSessionDto>> EndRegistrationAsync(Guid id, CancellationToken ct = default)
    {
        var session = await sessionRepository.GetByIdAsync(id, ct);
        if (session is null) return DomainErrors.AcademicSession.NotFound;

        session.IsRegistrationOpen = false;
        session.RegistrationEndDate = DateTime.UtcNow;

        await sessionRepository.UpdateAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        await LogActionAsync("EndRegistration", "AcademicSession", id.ToString(),
            $"Ended course registration for session {session.Name}", ct);

        return session.ToDto();
    }

    private async Task TriggerAutoRegistrationIfEnabledAsync(Guid sessionId, CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<LmsDbContext>();
            var autoRegService = scope.ServiceProvider.GetService<IAutoRegistrationService>();
            if (dbContext == null || autoRegService == null) return;

            var config = await dbContext.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
            if (config != null && config.EnableAutoRegistration && config.AutoRegisterOnRegistrationStart)
            {
                await autoRegService.RunBatchAutoRegistrationAsync(new BatchAutoRegistrationRequest
                {
                    AcademicSessionId = sessionId,
                    DryRun = false
                }, ct);
            }
        }
        catch
        {
            // Non-blocking background trigger
        }
    }
}
