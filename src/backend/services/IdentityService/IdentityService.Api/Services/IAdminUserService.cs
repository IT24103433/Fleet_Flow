using IdentityService.Api.Dtos;

namespace IdentityService.Api.Services;

public interface IAdminUserService
{
    Task<AdminUserResponse> CreateUserAsync(CreateAdminUserRequest request);
    Task<AdminUserResponse> UpdateUserAsync(Guid id, UpdateAdminUserRequest request, Guid currentUserId);
    Task<AdminUserResponse> SetUserStatusAsync(Guid id, bool isActive, Guid currentUserId);
    Task<AdminResetPasswordResponse> ResetPasswordAsync(Guid id, AdminResetPasswordRequest? request, Guid currentUserId = default);
    Task<AdminUserResponse> SetForcePasswordChangeAsync(Guid id, bool mustChangePassword, Guid currentUserId = default);
    Task DeleteUserAsync(Guid id, Guid currentUserId);
    Task<List<AdminUserResponse>> GetUsersAsync();
}

