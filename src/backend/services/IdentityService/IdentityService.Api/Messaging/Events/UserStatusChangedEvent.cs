using System;

namespace IdentityService.Api.Messaging.Events;

public class UserStatusChangedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = nameof(UserStatusChangedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid ChangedByAdminId { get; set; }
}
