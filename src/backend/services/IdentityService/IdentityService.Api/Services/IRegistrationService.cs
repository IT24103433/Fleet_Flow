using IdentityService.Api.Dtos;

namespace IdentityService.Api.Services;

public interface IRegistrationService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request);
}
