namespace FleetService.Api.Dtos;

public class UploadVehicleImageRequest
{
    public IFormFile? File { get; set; }
    public string? Caption { get; set; }
}
