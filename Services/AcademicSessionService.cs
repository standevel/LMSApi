using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

// Trigger watch rebuild
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
            IsActive = false,
            IsAdmissionActive = false,
            IsAdmissionOpen = request.IsAdmissionOpen,
            ActiveSemester = request.ActiveSemester
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
}
