namespace FleetFlow.Contracts;
public sealed record NotificationDelivery(Guid EventId, Guid TargetUserId, string Category, string Title,
    string Message, Guid RelatedEntityId, Guid VehicleId, DateTime CreatedAt);

public sealed record NotificationDeliveryAcknowledgement(Guid EventId,bool Processed);
