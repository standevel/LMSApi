using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IActiveDirectoryService
{
    Task<(string EntraObjectId, string OfficialEmail, string? TemporaryPassword, bool IsExisting)> CreateStudentAccountAsync(AdmissionApplication application);
}
