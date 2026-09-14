using System;
using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ITranscriptCoverLetterPdfService
{
    Task<byte[]> GenerateCoverLetterPdfAsync(TranscriptRequest request, string? templateType = "TranscriptCoverLetter");
    Task<byte[]> GenerateSampleCoverLetterPdfAsync(string? templateType = "TranscriptCoverLetter");
}
