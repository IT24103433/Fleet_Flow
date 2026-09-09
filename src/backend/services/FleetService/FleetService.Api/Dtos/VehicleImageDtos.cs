namespace FleetService.Api.Dtos;

public class VehicleImageResponse
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string RelativeUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UploadVehicleImageRequest
{
    public IFormFile? File { get; set; }
    public string? Caption { get; set; }
}
