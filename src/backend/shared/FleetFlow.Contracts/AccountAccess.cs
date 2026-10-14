namespace FleetFlow.Contracts;
public sealed record AccountAccessState(Guid UserId, long AccessVersion, bool IsActive, bool MustChangePassword);
