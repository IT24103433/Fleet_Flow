using IdentityService.Api.Data;
using IdentityService.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Services;

public class ProfileImageService : IProfileImageService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public ProfileImageService(
        IdentityDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
    }

    private string GetProfilesDirectory()
    {
        var uploadRoot = _configuration["IDENTITY_UPLOAD_ROOT"];
        if (string.IsNullOrWhiteSpace(uploadRoot))
        {
            uploadRoot = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads");
        }

        var dir = Path.Combine(uploadRoot, "profiles");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<string> UploadProfilePictureAsync(Guid userId, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("An image file must be provided.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("File size exceeds the 5 MB limit.");
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"Unsupported image type '{file.ContentType}'. Allowed types: JPEG, PNG, WebP.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            ext = file.ContentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            if (!IsValidImageHeader(bytes))
            {
                throw new ArgumentException("The file content is corrupted or not a recognized image.");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new NotFoundException($"User with ID '{userId}' was not found.");
            }

            var profilesDir = GetProfilesDirectory();

            // Clean up previous physical file if it exists
            if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
            {
                var oldFileName = Path.GetFileName(user.ProfileImageUrl);
                if (!string.IsNullOrWhiteSpace(oldFileName))
                {
                    var oldFilePath = Path.Combine(profilesDir, oldFileName);
                    if (File.Exists(oldFilePath))
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Storage Warning] Could not delete previous profile picture '{oldFilePath}': {ex.Message}");
                        }
                    }
                }
            }

            // Save new physical file
            var uniqueFileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var targetFilePath = Path.Combine(profilesDir, uniqueFileName);
            await File.WriteAllBytesAsync(targetFilePath, bytes);

            // Store portable relative URL in DB
            var relativeUrl = $"/uploads/profiles/{uniqueFileName}";
            user.ProfileImageUrl = relativeUrl;
            await _dbContext.SaveChangesAsync();

            return relativeUrl;
        }
    }

    public async Task<string?> GetProfilePictureUrlAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new NotFoundException($"User with ID '{userId}' was not found.");
        }

        return user.ProfileImageUrl;
    }

    public async Task DeleteProfilePictureAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new NotFoundException($"User with ID '{userId}' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            var profilesDir = GetProfilesDirectory();
            var fileName = Path.GetFileName(user.ProfileImageUrl);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var physicalPath = Path.Combine(profilesDir, fileName);
                if (File.Exists(physicalPath))
                {
                    try
                    {
                        File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Storage Warning] Could not delete profile picture '{physicalPath}': {ex.Message}");
                    }
                }
            }

            user.ProfileImageUrl = null;
            await _dbContext.SaveChangesAsync();
        }
    }

    private static bool IsValidImageHeader(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 12) return false;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return true;
        }

        // WEBP: 'RIFF' at 0..3 and 'WEBP' at 8..11
        if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return true;
        }

        return false;
    }
}
