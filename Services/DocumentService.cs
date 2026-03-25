using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LMS.Api.Services;

public sealed class DocumentService(LmsDbContext dbContext) : IDocumentService
{
    public async Task<DocumentRecord> UploadDocumentAsync(Guid? ownerId, Guid documentTypeId, string fileName, string fileUrl, long fileSize, string fileType, Guid? facultyId = null)
    {
        var record = new DocumentRecord
        {
            OwnerId = ownerId,
            DocumentTypeId = documentTypeId,
            FileName = fileName,
            FileUrl = fileUrl,
            FileSize = fileSize,
            FileType = fileType,
            FacultyId = facultyId,
            UploadedAt = DateTime.UtcNow
        };

        dbContext.DocumentRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    public async Task<bool> ValidateAccessAsync(Guid documentId, Guid userId, IEnumerable<string> userRoles)
    {
        var document = await dbContext.DocumentRecords
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        // 1. Owner always has access (if there is one)
        if (document.OwnerId != null && document.OwnerId == userId) return true;

        // 2. Admin role always has access
        if (userRoles.Contains("Admin") || userRoles.Contains("SuperAdmin")) return true;

        // 3. Process rules from DocumentType
        var rules = JsonSerializer.Deserialize<AccessRules>(document.DocumentType.DefaultAccessRules) ?? new();

        if (rules.AllowedRoles.Any(r => userRoles.Contains(r)))
        {
            // If faculty restricted, check faculty match
            if (rules.FacultyRestricted)
            {
                return document.FacultyId == null || document.FacultyId == (/* TODO: Get User Faculty */ Guid.Empty);
            }
            return true;
        }

        // 4. Process overrides from record metadata
        var overrides = JsonSerializer.Deserialize<AccessRules>(document.AccessMetadata) ?? new();
        if (overrides.AllowedRoles.Any(r => userRoles.Contains(r))) return true;

        return false;
    }

    public async Task<IEnumerable<DocumentType>> GetActiveDocumentTypesAsync(DocumentCategory? category = null)
    {
        var query = dbContext.DocumentTypes.Where(t => t.IsActive);
        if (category.HasValue)
        {
            query = query.Where(t => t.Category == category.Value);
        }
        return await query.ToListAsync();
    }

    public async Task<DocumentRecord?> GetDocumentByIdAsync(Guid id)
    {
        return await dbContext.DocumentRecords
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DocumentRecord> UpdateDocumentStatusAsync(Guid id, DocumentStatus status, string? rejectionReason = null)
    {
        var record = await dbContext.DocumentRecords.FindAsync(id);
        if (record == null) throw new KeyNotFoundException("Document not found.");

        record.Status = status;
        record.RejectionReason = rejectionReason;
        
        await dbContext.SaveChangesAsync();
        return record;
    }

    private class AccessRules
    {
        public List<string> AllowedRoles { get; set; } = new();
        public bool AllowOwner { get; set; } = true;
        public bool FacultyRestricted { get; set; }
    }
}
