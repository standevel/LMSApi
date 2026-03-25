using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ILetterTemplateService
{
    Task<LetterTemplateResponse> SaveTemplateAsync(SaveLetterTemplateRequest request);
    Task<LetterTemplateResponse?> GetTemplateByTypeAsync(string templateType);
    Task<IEnumerable<LetterTemplateResponse>> GetAllTemplatesAsync();
}
