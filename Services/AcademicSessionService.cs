using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

public sealed class AcademicSessionService(
    IAcademicSessionRepository sessionRepository,
    IAuditService auditService) : BaseService(auditService), IAcademicSessionService
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
            IsActive = false
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

        session.Name = request.Name;
        session.StartDate = request.StartDate;
        session.EndDate = request.EndDate;

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
}
