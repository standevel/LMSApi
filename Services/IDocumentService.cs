using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IDocumentService
{
    Task<DocumentRecord> UploadDocumentAsync(Guid? ownerId, Guid documentTypeId, string fileName, string fileUrl, long fileSize, string fileType, Guid? facultyId = null);
    Task<bool> ValidateAccessAsync(Guid documentId, Guid userId, IEnumerable<string> userRoles);
    Task<IEnumerable<DocumentType>> GetActiveDocumentTypesAsync(DocumentCategory? category = null);
    Task<DocumentRecord?> GetDocumentByIdAsync(Guid id);
    Task<DocumentRecord> UpdateDocumentStatusAsync(Guid id, DocumentStatus status, string? rejectionReason = null);
}
