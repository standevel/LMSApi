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
        return MapBankToDto(qb);
    }

    public async Task<ErrorOr<List<QuestionBankDto>>> GetQuestionBanksByCourseAsync(Guid? courseOfferingId, CancellationToken ct = default)
    {
        var query = _context.QuestionBanks.AsQueryable();
        
        if (courseOfferingId.HasValue && courseOfferingId.Value != Guid.Empty)
            query = query.Where(qb => qb.CourseOfferingId == courseOfferingId.Value);
        
        var banks = await query
            .Include(qb => qb.Items)
            .ToListAsync(ct);
        return banks.Select(qb => MapBankToDto(qb)).ToList();
    }

    public async Task<ErrorOr<QuestionBankDto>> GetQuestionBankByIdAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var qb = await _context.QuestionBanks
            .Include(qb => qb.Items)
            .ThenInclude(item => item.Options)
            .FirstOrDefaultAsync(qb => qb.Id == questionBankId, ct);
        return qb == null ? Error.NotFound("QuestionBank.NotFound", "Question bank not found") : MapBankToDto(qb, includeItems: true);
    }

    public async Task<ErrorOr<QuestionBankDto>> UpdateQuestionBankAsync(Guid questionBankId, string name, string description, Guid? courseOfferingId, CancellationToken ct = default)
    {
        var qb = await _context.QuestionBanks.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == questionBankId, ct);
        if (qb == null) return Error.NotFound("QuestionBank.NotFound", "Question bank not found");
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("InvalidInput", "Name is required for the question bank.");

        qb.Name = name;
        qb.Description = description;
        qb.CourseOfferingId = courseOfferingId;
        qb.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return MapBankToDto(qb);
    }

    public async Task<ErrorOr<Deleted>> DeleteQuestionBankAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var qb = await _context.QuestionBanks.FirstOrDefaultAsync(x => x.Id == questionBankId, ct);
        if (qb == null) return Error.NotFound("QuestionBank.NotFound", "Question bank not found");

        _context.QuestionBanks.Remove(qb);
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<QuestionBankItemDto>>> GetQuestionBankItemsAsync(Guid questionBankId, string? search, string? type, string? difficulty, string? category, CancellationToken ct = default)
    {
        var exists = await _context.QuestionBanks.AnyAsync(qb => qb.Id == questionBankId, ct);
        if (!exists) return Error.NotFound("QuestionBank.NotFound", "Question bank not found");

        var query = _context.QuestionBankItems
            .Include(item => item.Options)
            .Where(item => item.QuestionBankId == questionBankId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.QuestionText.Contains(term) || (item.Tags != null && item.Tags.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(item => item.QuestionType == type);
        if (!string.IsNullOrWhiteSpace(difficulty)) query = query.Where(item => item.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category);

        var items = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(ct);
        return items.Select(MapItemToDto).ToList();
    }

    public async Task<ErrorOr<QuestionBankItemDto>> CreateQuestionBankItemAsync(Guid questionBankId, CreateQuestionBankItemRequest request, Guid createdBy, CancellationToken ct = default)
    {
        var bank = await _context.QuestionBanks.FirstOrDefaultAsync(qb => qb.Id == questionBankId, ct);
        if (bank == null) return Error.NotFound("QuestionBank.NotFound", "Question bank not found");
        if (string.IsNullOrWhiteSpace(request.QuestionText)) return Error.Validation("InvalidInput", "Question text is required.");

        var item = new QuestionBankItem
        {
            QuestionBankId = questionBankId,
            QuestionText = request.QuestionText,
            QuestionType = request.QuestionType,
            Points = request.Points ?? 1,
            Category = request.Category,
            Difficulty = request.Difficulty,
            Tags = request.Tags,
            Explanation = request.Explanation,
            Feedback = request.Feedback,
            CreatedBy = createdBy,
            Options = request.Options.Select((option, index) => new QuestionBankOption
            {
                OptionText = option.OptionText,
                DisplayOrder = option.DisplayOrder > 0 ? option.DisplayOrder : index + 1,
                IsCorrectAnswer = option.IsCorrectAnswer
            }).ToList()
        };

        var correct = item.Options.FirstOrDefault(o => o.IsCorrectAnswer);
        item.CorrectAnswer = correct?.OptionText;

        _context.QuestionBankItems.Add(item);
        await _context.SaveChangesAsync(ct);
        return MapItemToDto(item);
    }

    public async Task<ErrorOr<QuestionBankItemDto>> UpdateQuestionBankItemAsync(Guid itemId, UpdateQuestionBankItemRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var item = await _context.QuestionBankItems
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item == null) return Error.NotFound("QuestionBankItem.NotFound", "Question bank item not found");

        if (!string.IsNullOrWhiteSpace(request.QuestionText)) item.QuestionText = request.QuestionText;
        if (!string.IsNullOrWhiteSpace(request.QuestionType)) item.QuestionType = request.QuestionType;
        if (request.Points.HasValue) item.Points = request.Points;
        if (request.Category != null) item.Category = request.Category;
        if (request.Difficulty != null) item.Difficulty = request.Difficulty;
        if (request.Tags != null) item.Tags = request.Tags;
        if (request.Explanation != null) item.Explanation = request.Explanation;
        if (request.Feedback != null) item.Feedback = request.Feedback;

        if (request.Options != null)
        {
            _context.QuestionBankOptions.RemoveRange(item.Options);
            item.Options = request.Options.Select((option, index) => new QuestionBankOption
            {
                QuestionBankItemId = item.Id,
                OptionText = option.OptionText,
                DisplayOrder = index + 1,
                IsCorrectAnswer = option.IsCorrectAnswer
            }).ToList();
        }

        item.CorrectAnswer = item.Options.FirstOrDefault(o => o.IsCorrectAnswer)?.OptionText;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = updatedBy;
        await _context.SaveChangesAsync(ct);
        return MapItemToDto(item);
    }

    public async Task<ErrorOr<QuestionBankItemDto>> DuplicateQuestionBankItemAsync(Guid itemId, Guid createdBy, CancellationToken ct = default)
    {
        var source = await _context.QuestionBankItems
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (source == null) return Error.NotFound("QuestionBankItem.NotFound", "Question bank item not found");

        var copy = new QuestionBankItem
        {
            QuestionBankId = source.QuestionBankId,
            QuestionText = $"{source.QuestionText} (copy)",
            QuestionType = source.QuestionType,
            Points = source.Points,
            Category = source.Category,
            Difficulty = source.Difficulty,
            Tags = source.Tags,
            Explanation = source.Explanation,
            Feedback = source.Feedback,
            CorrectAnswer = source.CorrectAnswer,
            CreatedBy = createdBy,
            Options = source.Options.OrderBy(o => o.DisplayOrder).Select(o => new QuestionBankOption
            {
                OptionText = o.OptionText,
                DisplayOrder = o.DisplayOrder,
                IsCorrectAnswer = o.IsCorrectAnswer
            }).ToList()
        };

        _context.QuestionBankItems.Add(copy);
        await _context.SaveChangesAsync(ct);
        return MapItemToDto(copy);
    }

    public async Task<ErrorOr<Deleted>> DeleteQuestionBankItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await _context.QuestionBankItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item == null) return Error.NotFound("QuestionBankItem.NotFound", "Question bank item not found");

        _context.QuestionBankItems.Remove(item);
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    private static QuestionBankDto MapBankToDto(QuestionBank bank, bool includeItems = false)
    {
        var dto = new QuestionBankDto(bank.Id, bank.Name, bank.Description, bank.CourseOfferingId)
        {
            ItemCount = bank.Items?.Count ?? 0
        };

        if (includeItems && bank.Items != null)
        {
            dto.Items = bank.Items.OrderByDescending(item => item.CreatedAt).Select(MapItemToDto).ToList();
        }

        return dto;
    }

    private static QuestionBankItemDto MapItemToDto(QuestionBankItem item) => new()
    {
        Id = item.Id,
        QuestionBankId = item.QuestionBankId,
        QuestionText = item.QuestionText,
        QuestionType = item.QuestionType ?? "MCQ",
        Points = item.Points,
        Options = item.Options.OrderBy(o => o.DisplayOrder).Select(o => new QuestionBankOptionDto
        {
            Id = o.Id,
            OptionText = o.OptionText,
            DisplayOrder = o.DisplayOrder,
            IsCorrectAnswer = o.IsCorrectAnswer
        }).ToList(),
        CorrectAnswer = item.CorrectAnswer,
        Category = item.Category,
        Difficulty = item.Difficulty,
        Tags = item.Tags,
        Explanation = item.Explanation,
        Feedback = item.Feedback,
        TimesUsed = item.TimesUsed,
        AverageScore = item.AverageScore,
        CreatedAt = item.CreatedAt
    };
}
