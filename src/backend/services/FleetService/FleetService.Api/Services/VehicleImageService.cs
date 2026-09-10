using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Api.Services;

public class VehicleImageService : IVehicleImageService
{
    private readonly FleetDbContext _dbContext;
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

    public VehicleImageService(
        FleetDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
    }

    private string GetVehiclesDirectory()
    {
        var uploadRoot = _configuration["FLEET_UPLOAD_ROOT"];
        if (string.IsNullOrWhiteSpace(uploadRoot))
        {
            uploadRoot = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads");
        }

        var dir = Path.Combine(uploadRoot, "vehicles");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<List<VehicleImageResponse>> GetImagesAsync(Guid vehicleId)
    {
        var vehicleExists = await _dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId);
        if (!vehicleExists)
        {
            throw new NotFoundException($"Vehicle with ID '{vehicleId}' was not found.");
        }

        var images = await _dbContext.VehicleImages
            .Where(i => i.VehicleId == vehicleId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();

        return images.Select(i => new VehicleImageResponse
        {
            Id = i.Id,
            VehicleId = i.VehicleId,
            FileName = i.FileName,
            OriginalFileName = i.OriginalFileName,
            ContentType = i.ContentType,
            FileSize = i.FileSize,
            RelativeUrl = i.RelativeUrl,
            Caption = i.Caption,
            CreatedAt = i.CreatedAt
        }).ToList();
    }

    public async Task<VehicleImageResponse> UploadImageAsync(Guid vehicleId, IFormFile? file, string? caption)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("An image file must be provided.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("File size exceeds the 5 MB limit.");
        }

        // Validate MIME type
        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"Unsupported image type '{file.ContentType}'. Allowed types: JPEG, PNG, WebP.");
        }

        // Validate file extension
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

        // Validate magic byte signatures
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            if (!IsValidImageHeader(bytes))
            {
                throw new ArgumentException("The file content is corrupted or not a recognized image.");
            }

            // Check that target vehicle exists
            var vehicleExists = await _dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId);
            if (!vehicleExists)
            {
                throw new NotFoundException($"Vehicle with ID '{vehicleId}' was not found.");
            }

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var vehiclesDir = GetVehiclesDirectory();
            var targetFilePath = Path.Combine(vehiclesDir, uniqueFileName);

            // Save to physical file
            await File.WriteAllBytesAsync(targetFilePath, bytes);

            // Relative URL for API & static file serving
            var relativeUrl = $"/uploads/vehicles/{uniqueFileName}";

            var imageEntity = new VehicleImage
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                FileName = uniqueFileName,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                FileSize = file.Length,
                RelativeUrl = relativeUrl,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.VehicleImages.Add(imageEntity);
            await _dbContext.SaveChangesAsync();

            return new VehicleImageResponse
            {
                Id = imageEntity.Id,
                VehicleId = imageEntity.VehicleId,
                FileName = imageEntity.FileName,
                OriginalFileName = imageEntity.OriginalFileName,
                ContentType = imageEntity.ContentType,
                FileSize = imageEntity.FileSize,
                RelativeUrl = imageEntity.RelativeUrl,
                Caption = imageEntity.Caption,
                CreatedAt = imageEntity.CreatedAt
            };
        }
    }

    public async Task DeleteImageAsync(Guid vehicleId, Guid imageId)
    {
        var image = await _dbContext.VehicleImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.VehicleId == vehicleId);

        if (image == null)
        {
            throw new NotFoundException($"Image with ID '{imageId}' for vehicle '{vehicleId}' was not found.");
        }

        // Delete physical file safely resolving from configured upload root
        var vehiclesDir = GetVehiclesDirectory();
        var physicalPath = Path.Combine(vehiclesDir, image.FileName);
        if (File.Exists(physicalPath))
        {
            try
            {
                File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Storage Warning] Could not remove physical file '{physicalPath}': {ex.Message}");
            }
        }

        // Remove DB record
        _dbContext.VehicleImages.Remove(image);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAllImagesForVehicleAsync(Guid vehicleId)
    {
        var images = await _dbContext.VehicleImages
            .Where(i => i.VehicleId == vehicleId)
            .ToListAsync();

        if (images.Count == 0) return;

        var vehiclesDir = GetVehiclesDirectory();
        foreach (var image in images)
        {
            var physicalPath = Path.Combine(vehiclesDir, image.FileName);
            if (File.Exists(physicalPath))
            {
                try
                {
                    File.Delete(physicalPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Storage Warning] Could not remove physical file '{physicalPath}': {ex.Message}");
                }
            }
        }

        _dbContext.VehicleImages.RemoveRange(images);
        await _dbContext.SaveChangesAsync();
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
