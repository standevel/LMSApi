using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IActiveDirectoryService
{
    Task<(string OfficialEmail, string TemporaryPassword)> CreateStudentAccountAsync(AdmissionApplication application);
}
