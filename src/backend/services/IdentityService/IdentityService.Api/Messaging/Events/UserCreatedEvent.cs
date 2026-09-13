using System;
using System.Collections.Generic;

namespace IdentityService.Api.Messaging.Events;

public class UserCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = nameof(UserCreatedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
