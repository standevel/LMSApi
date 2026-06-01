using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class QuestionBankService : BaseService, IQuestionBankService
{
    private readonly LmsDbContext _context;

    public QuestionBankService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<QuestionBankDto>> CreateQuestionBankAsync(string name, string description, Guid? courseOfferingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("InvalidInput", "Name is required for the question bank.");

        var qb = new QuestionBank
        {
            Name = name,
            Description = description,
            CourseOfferingId = courseOfferingId
        };
        _context.QuestionBanks.Add(qb);
        await _context.SaveChangesAsync(ct);
        return new QuestionBankDto(qb.Id, qb.Name, qb.Description, qb.CourseOfferingId);
    }

    public async Task<ErrorOr<List<QuestionBankDto>>> GetQuestionBanksByCourseAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var qbDtos = await _context.QuestionBanks
            .Where(qb => qb.CourseOfferingId == courseOfferingId)
            .Select(qb => new QuestionBankDto(qb.Id, qb.Name, qb.Description, qb.CourseOfferingId))
            .ToListAsync(ct);
        return qbDtos;
    }

    public async Task<ErrorOr<QuestionBankDto>> GetQuestionBankByIdAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var qb = await _context.QuestionBanks
            .Include(qb => qb.Questions)
            .FirstOrDefaultAsync(qb => qb.Id == questionBankId, ct);
        return qb == null ? Error.NotFound("QuestionBank.NotFound", "Question bank not found") : new QuestionBankDto(qb.Id, qb.Name, qb.Description, qb.CourseOfferingId);
    }
}
