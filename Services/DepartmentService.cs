using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class DepartmentService(
    IDepartmentRepository departmentRepository,
    IFacultyRepository facultyRepository,
    IUserRoleRepository userRoleRepository,
    IRoleRepository roleRepository,
    LmsDbContext dbContext,
    IAuditService auditService) : BaseService(auditService), IDepartmentService
{
    public async Task<ErrorOr<DepartmentDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var department = await departmentRepository.GetByIdAsync(id, ct);
        if (department is null) return DomainErrors.Department.NotFound;

        return department.ToDto();
    }

    public async Task<ErrorOr<List<DepartmentDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var departments = await departmentRepository.GetAllAsync(ct);
        return departments.Select(d => d.ToDto()).ToList();
    }

    public async Task<ErrorOr<List<DepartmentDto>>> GetByFacultyIdAsync(Guid facultyId, CancellationToken ct = default)
    {
        var departments = await dbContext.Departments
            .Where(d => d.FacultyId == facultyId)
            .Include(d => d.Faculty)
            .ToListAsync(ct);
        return departments.Select(d => d.ToDto()).ToList();
    }

    public async Task<ErrorOr<DepartmentDto>> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default)
    {
        if (await dbContext.Departments.AnyAsync(d => d.Code == request.Code, ct))
            return DomainErrors.Department.DuplicateCode;

        var faculty = await facultyRepository.GetByIdAsync(request.FacultyId, ct);
        if (faculty is null) return DomainErrors.Faculty.NotFound;

        var department = new Department
        {
            Name = request.Name,
            Code = request.Code,
            FacultyId = request.FacultyId,
            HeadId = request.HeadId
        };

        await departmentRepository.AddAsync(department, ct);
        await departmentRepository.SaveChangesAsync(ct);

        if (department.HeadId.HasValue)
        {
            await AssignRoleAsync(department.HeadId.Value, LmsRoles.HOD, ct);
            await AssignRoleAsync(department.HeadId.Value, LmsRoles.Lecturer, ct);
            
            var user = await dbContext.Users.FindAsync([department.HeadId.Value], ct);
            if (user != null)
            {
                user.DepartmentId = department.Id;
            }
        }

        await LogActionAsync("Create", "Department", department.Id.ToString(), $"Created department: {department.Name} ({department.Code})", ct);

        return department.ToDto();
    }

    public async Task<ErrorOr<DepartmentDto>> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var department = await departmentRepository.GetByIdAsync(id, ct);
        if (department is null) return DomainErrors.Department.NotFound;

        var faculty = await facultyRepository.GetByIdAsync(request.FacultyId, ct);
        if (faculty is null) return DomainErrors.Faculty.NotFound;

        var oldHeadId = department.HeadId;

        department.Name = request.Name;
        department.Code = request.Code;
        department.FacultyId = request.FacultyId;
        department.HeadId = request.HeadId;
        department.UpdatedDate = DateOnly.FromDateTime(DateTime.Now);

        await departmentRepository.UpdateAsync(department, ct);
        await departmentRepository.SaveChangesAsync(ct);

        if (oldHeadId != request.HeadId)
        {
            if (oldHeadId.HasValue)
            {
                await RevokeRoleAsync(oldHeadId.Value, LmsRoles.HOD, ct);
            }
            
            if (request.HeadId.HasValue)
            {
                await AssignRoleAsync(request.HeadId.Value, LmsRoles.HOD, ct);
                await AssignRoleAsync(request.HeadId.Value, LmsRoles.Lecturer, ct);
                
                var user = await dbContext.Users.FindAsync([request.HeadId.Value], ct);
                if (user != null)
                {
                    user.DepartmentId = department.Id;
                }
            }
        }

        await LogActionAsync("Update", "Department", id.ToString(), $"Updated department: {department.Name}", ct);

        return department.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var department = await departmentRepository.GetByIdAsync(id, ct);
        if (department is null) return DomainErrors.Department.NotFound;

        await departmentRepository.DeleteAsync(department, ct);
        await departmentRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Department", id.ToString(), $"Deleted department: {department.Name}", ct);

        return Result.Deleted;
    }

    private async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        var role = await roleRepository.GetByNameAsync(roleName, ct);
        if (role is not null)
        {
            var exists = await userRoleRepository.AssignmentExistsAsync(userId, role.Id, ct);
            if (!exists)
            {
                await userRoleRepository.AssignAsync(userId, role.Id, DateTime.UtcNow, ct);
                await userRoleRepository.SaveChangesAsync(ct);
            }
        }
    }

    private async Task RevokeRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        var role = await roleRepository.GetByNameAsync(roleName, ct);
        if (role is not null)
        {
            var exists = await userRoleRepository.AssignmentExistsAsync(userId, role.Id, ct);
            if (exists)
            {
                await userRoleRepository.RevokeAsync(userId, role.Id, ct);
                await userRoleRepository.SaveChangesAsync(ct);
            }
        }
    }
}
