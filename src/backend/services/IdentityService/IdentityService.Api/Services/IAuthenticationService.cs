using System.Threading.Tasks;
using IdentityService.Api.Dtos;

namespace IdentityService.Api.Services;

public interface IAuthenticationService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
}
