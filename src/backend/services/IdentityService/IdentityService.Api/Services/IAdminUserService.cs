using IdentityService.Api.Dtos;

namespace IdentityService.Api.Services;

public interface IAdminUserService
{
    Task<AdminUserResponse> CreateUserAsync(CreateAdminUserRequest request);
    Task<AdminUserResponse> UpdateUserAsync(Guid id, UpdateAdminUserRequest request, Guid currentUserId);
    Task DeleteUserAsync(Guid id, Guid currentUserId);
    Task<List<AdminUserResponse>> GetUsersAsync();
}

