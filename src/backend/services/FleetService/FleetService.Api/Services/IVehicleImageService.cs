using FleetService.Api.Dtos;

namespace FleetService.Api.Services;

public interface IVehicleImageService
{
    Task<List<VehicleImageResponse>> GetImagesAsync(Guid vehicleId);
    Task<VehicleImageResponse> UploadImageAsync(Guid vehicleId, IFormFile file, string? caption);
    Task DeleteImageAsync(Guid vehicleId, Guid imageId);
    Task DeleteAllImagesForVehicleAsync(Guid vehicleId);
}
