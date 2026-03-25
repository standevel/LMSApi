using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IPdfService
{
    Task<byte[]> GenerateOfferLetterAsync(AdmissionApplication application, string? templateType = "Undergraduate");
}
