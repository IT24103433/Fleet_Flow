namespace RentalService.Api.Data;
public sealed class RentalReturn
{
    public Guid RentalId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public int Odometer { get; set; }
    public int FuelPercentage { get; set; }
    public string Condition { get; set; } = "";
    public Guid CompletedByUserId { get; set; }
    public decimal ExtraCharges { get; set; }
    public string? ExtraChargeReason { get; set; }
    public decimal FinalAmount { get; set; }
    public bool IsLate { get; set; }
}
public sealed class RentalLifecycleEvent
{
    public Guid BookingId { get; set; }
    public string Kind { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public Guid ActorId { get; set; }
}
