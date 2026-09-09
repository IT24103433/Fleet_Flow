namespace IdentityService.Api.Services;

public interface IProfileImageService
{
    Task<string> UploadProfilePictureAsync(Guid userId, IFormFile file);
    Task<string?> GetProfilePictureUrlAsync(Guid userId);
    Task DeleteProfilePictureAsync(Guid userId);
}
