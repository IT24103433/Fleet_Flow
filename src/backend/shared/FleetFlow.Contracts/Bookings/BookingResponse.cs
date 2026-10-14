using System;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public class BookingResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public BookingStatus Status { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public Guid? RentalId {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public FleetFlow.Contracts.RentalLifecycleState? RentalState {get;set;}
    public VehicleResponse? Vehicle { get; set; }
}
