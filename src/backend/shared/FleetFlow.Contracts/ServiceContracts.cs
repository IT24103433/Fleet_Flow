using System.ComponentModel.DataAnnotations;
namespace FleetFlow.Contracts;

public sealed record ServiceTokenRequest([property: Required, MaxLength(100)] string ClientId,
    [property: Required, MaxLength(512)] string ClientSecret);
public sealed record ServiceTokenResponse(string AccessToken, string TokenType, int ExpiresIn);
public sealed record ServiceInformation(string Service, string Database, int SchemaVersion, bool BusinessActivated);

// Contract identifiers survive retries and migration. These contain references,
// never shared persistence models or another service's database connection.
public sealed record ReservationRequest(Guid OperationId, Guid BookingId, Guid VehicleId,
    Guid CustomerId, DateTimeOffset StartDateTime, DateTimeOffset EndDateTime);
public sealed record ReservationReference(Guid OperationId, Guid ReservationId, Guid BookingId, Guid VehicleId);
public sealed record IntegrationEventEnvelope(Guid EventId, int SchemaVersion, string EventType,
    DateTimeOffset OccurredAt, string Producer, string Payload);

public static class ServiceScopes
{
    public const string FleetRead = "fleet.read";
    public const string ReservationWrite = "fleet.reservations.write";
    public const string RentalReportsRead = "rental.reports.read";
    public const string IdentityAccessRead = "identity.access.read";
    public const string IdentityStaffRead = "identity.staff.read";
    public const string NotificationOperations = "notifications.operations";
    public static readonly IReadOnlyDictionary<string, string[]> ClientGrants =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["rental-service"] = [FleetRead, ReservationWrite, IdentityAccessRead],
            ["fleet-service"] = [RentalReportsRead, IdentityAccessRead, IdentityStaffRead],
            ["notification-service"] = [IdentityAccessRead, NotificationOperations]
        };
}
