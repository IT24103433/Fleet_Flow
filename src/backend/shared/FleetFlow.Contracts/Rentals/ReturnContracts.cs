using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace FleetFlow.Contracts;
public sealed class ReturnRequest
{
    [JsonRequired, Range(0, int.MaxValue)] public int Odometer { get; set; }
    [JsonRequired, Range(0, 100)] public int FuelPercentage { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Condition { get; set; } = "";
    [Range(typeof(decimal), "0", "1000000000")] public decimal ExtraCharges { get; set; }
    [StringLength(500)] public string? ExtraChargeReason { get; set; }
}
public sealed record RentalCompletionCommand(Guid BookingId, Guid RentalId, int Odometer, DateTime ReturnedAt);
public sealed record RentalCompletionAcknowledgement(Guid BookingId, Guid RentalId, bool Released);
public sealed record RentalReturnResponse(DateTime ReturnedAt, int Odometer, int FuelPercentage, string Condition,
    Guid CompletedByUserId, decimal ExtraCharges, string? ExtraChargeReason, decimal FinalAmount, bool IsLate);
public sealed record RentalTimelineItem(string Kind, string Title, DateTime? OccurredAt, string? Details, Guid? ActorId);
public sealed record RentalTimelineResponse(Guid BookingId, Guid? RentalId, IReadOnlyList<RentalTimelineItem> Items);
