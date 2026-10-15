namespace FleetFlow.Contracts;

public sealed record DriverEligibility(bool Enabled, bool Eligible, string Status, string? Reason, Guid? Revision, DateTime? ValidUntil);
public sealed record AccountObligations(bool HasOutstandingRentals);
public sealed record AccountLifecycleCommand(Guid OperationId);
public sealed record TransactionalEmail(Guid EventId, Guid? UserId, string Recipient, string Template, string Subject,
    string Message, string? ActionUrl = null, DateTime? ExpiresAt = null, string? ReplyTo = null);

public static class OnboardingScopes
{
    public const string DriverEligibility = "identity.driver-eligibility.read";
    public const string EmailEnqueue = "notifications.email.enqueue";
    public const string AccountObligations = "rental.account-obligations.read";
    public const string ContactRead = "identity.notification-contact.read";
    public const string RentalLifecycle = "rental.account-lifecycle.manage";
    public const string NotificationLifecycle = "notifications.account-lifecycle.manage";
}
