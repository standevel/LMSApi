using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Security;
using ErrorOr;

namespace LMS.Api.Endpoints.SelfService;

public record UpdateMatricNumberRequest(string MatricNumber);

public sealed class UpdateStudentMatricNumberEndpoint : ApiEndpoint<UpdateMatricNumberRequest, bool>
{
    private readonly LmsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateStudentMatricNumberEndpoint(LmsDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("students/me/matric-number");
        AllowAnonymous();
        Tags("SelfService");
    }

    public override async Task HandleAsync(UpdateMatricNumberRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.MatricNumber))
        {
            await SendAsync(Error.Validation("MatricNumber.Required", "Matriculation number is required."), ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (userId == null)
        {
            await SendAsync(Error.Unauthorized("User.Unauthorized", "User is not authenticated."), ct);
            return;
        }

        var student = await _dbContext.Students
            .FirstOrDefaultAsync(s => s.Id == userId.Value, ct);

        if (student == null)
        {
            await SendAsync(Error.NotFound("Student.NotFound", "Student profile not found."), ct);
            return;
        }

        if (student.AdmissionApplicationId != null || !string.IsNullOrWhiteSpace(student.StudentNumber))
        {
            await SendAsync(Error.Forbidden("MatricNumber.NotEligible", "You are not eligible to set your matriculation number or it has already been set."), ct);
            return;
        }

        var isDuplicate = await _dbContext.Students
            .AnyAsync(s => s.StudentNumber == req.MatricNumber && s.Id != student.Id, ct);

        if (isDuplicate)
        {
            await SendAsync(Error.Conflict("MatricNumber.Duplicate", "This matriculation number is already in use by another student."), ct);
            return;
        }

        student.StudentNumber = req.MatricNumber;
        student.UpdatedAt = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Action = "SelfSupplyMatricNumber",
            EntityName = "Student",
            EntityId = student.Id.ToString(),
            Changes = $"Student self-supplied Matric Number '{req.MatricNumber}' via dashboard.",
            Timestamp = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(true, ct);
    }
}
