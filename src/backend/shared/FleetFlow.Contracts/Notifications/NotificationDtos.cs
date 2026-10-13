using System.ComponentModel.DataAnnotations;

namespace FleetService.Api.Dtos;

public record NotificationResponse(Guid Id, string Category, string Title, string Message,
    Guid? RelatedEntityId, Guid? VehicleId, DateTime CreatedAt, bool IsRead);

public class CreateSystemNotificationRequest
{
    public Guid TargetUserId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Message { get; set; } = string.Empty;
}
