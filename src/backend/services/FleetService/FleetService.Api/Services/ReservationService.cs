using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Services;
public sealed class ReservationService(FleetDbContext db,IVehicleService vehicles)
{
 public async Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand command,CancellationToken ct=default)
 {
  if(command.BookingId==Guid.Empty||command.VehicleId==Guid.Empty||command.CustomerId==Guid.Empty)throw new ValidationException("Reservation identifiers are required.");
  var start=RentalPeriod.Utc(command.StartDateTime);var end=RentalPeriod.Utc(command.EndDateTime);
  if(RentalPeriod.Error(start,end,false) is {} error)throw new ValidationException(error);
  await using var tx=await VehicleWriteTransaction.BeginAsync(db,command.VehicleId,ct);
  var existing=await db.VehicleReservations.SingleOrDefaultAsync(r=>r.BookingId==command.BookingId,ct);
  if(existing!=null)
  {
   if(existing.VehicleId!=command.VehicleId||existing.CustomerId!=command.CustomerId||existing.StartDateTime!=start||existing.EndDateTime!=end||existing.Released)
    throw new DuplicateException("Reservation identifier is already used by a different or released reservation.");
   return Ack(existing);
  }
  var vehicle=await vehicles.GetVehicleByIdAsync(command.VehicleId)??throw new NotFoundException("Vehicle was not found.");
  if(vehicle.Status!=VehicleStatus.Available)throw new ValidationException($"Vehicle is currently in '{vehicle.Status}' status and is not available for rental booking.");
  if(await MaintenanceAvailability.Blocking(db,command.VehicleId,start,end).AnyAsync(ct) ||
   await db.VehicleReservations.AnyAsync(r=>r.VehicleId==command.VehicleId&&!r.Released&&(r.IsHeld || r.StartDateTime<end&&r.EndDateTime>start),ct))
    throw new DuplicateException("The selected vehicle has an overlapping booking or maintenance for the requested timeframe.");
  var row=new VehicleReservation{BookingId=command.BookingId,VehicleId=command.VehicleId,CustomerId=command.CustomerId,StartDateTime=start,EndDateTime=end,
   TotalCost=RentalPeriod.Estimate(start,end,vehicle.DailyRate),VehicleSnapshot=JsonSerializer.Serialize(vehicle),CreatedAt=DateTime.UtcNow};
  db.VehicleReservations.Add(row);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return Ack(row);
 }
 public async Task<ReservationAcknowledgement?> ReleaseAsync(Guid bookingId,CancellationToken ct=default)
 {
  var vehicleId=await db.VehicleReservations.AsNoTracking().Where(r=>r.BookingId==bookingId).Select(r=>(Guid?)r.VehicleId).SingleOrDefaultAsync(ct);
  if(vehicleId==null)return null;
  await using var tx=await VehicleWriteTransaction.BeginAsync(db,vehicleId.Value,ct);
  var row=await db.VehicleReservations.SingleAsync(r=>r.BookingId==bookingId,ct);
  if(row.IsHeld)throw new ValidationException("An active rental cannot be released through cancellation.");
  if(!row.Released){row.Released=true;row.ReleasedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
  if(tx!=null)await tx.CommitAsync(ct);return Ack(row);
 }
 public async Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand command,CancellationToken ct=default)
 {
  if(command.RentalId==Guid.Empty||command.Odometer<0||command.PickupAt.Kind!=DateTimeKind.Utc)throw new ValidationException("Valid rental, odometer and UTC pickup time are required.");
  var vehicleId=await db.VehicleReservations.AsNoTracking().Where(r=>r.BookingId==command.BookingId).Select(r=>(Guid?)r.VehicleId).SingleOrDefaultAsync(ct);
  if(vehicleId==null)throw new NotFoundException("Reservation was not found.");
  await using var tx=await VehicleWriteTransaction.BeginAsync(db,vehicleId.Value,ct);
  var row=await db.VehicleReservations.SingleAsync(r=>r.BookingId==command.BookingId,ct);
  var at=RentalPeriod.Utc(command.PickupAt);
  if(row.IsHeld)
  {
   if(row.HeldRentalId!=command.RentalId||row.PickupOdometer!=command.Odometer||row.PickupAt!=at)throw new DuplicateException("Pickup command differs from the saved handover.");
   return new(row.BookingId,command.RentalId,true);
  }
  if(row.Released||at<row.StartDateTime||at>=row.EndDateTime)throw new ValidationException("Pickup must be within the confirmed booking period.");
  var vehicle=await db.Vehicles.SingleAsync(v=>v.Id==row.VehicleId,ct);
  if(vehicle.Status!=VehicleStatus.Available||await db.VehicleReservations.AnyAsync(r=>r.VehicleId==row.VehicleId&&r.BookingId!=row.BookingId&&r.IsHeld,ct)||
   await MaintenanceAvailability.Blocking(db,row.VehicleId,at,row.EndDateTime).AnyAsync(ct))
   throw new DuplicateException("Vehicle cannot be handed over while in use or under maintenance.");
  if(command.Odometer<vehicle.Mileage)throw new ValidationException("Pickup odometer cannot be lower than the vehicle's recorded mileage.");
  row.IsHeld=true;row.HeldRentalId=command.RentalId;row.PickupAt=at;row.PickupOdometer=command.Odometer;
  vehicle.Status=VehicleStatus.InUse;vehicle.Mileage=command.Odometer;vehicle.UpdatedAt=DateTime.UtcNow;
  await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return new(row.BookingId,command.RentalId,true);
 }
 public async Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand command,CancellationToken ct=default)
 {
  if(command.RentalId==Guid.Empty||command.Odometer<0||command.ReturnedAt.Kind!=DateTimeKind.Utc)throw new ValidationException("Valid rental, mileage and UTC return time are required.");
  var vehicleId=await db.VehicleReservations.AsNoTracking().Where(r=>r.BookingId==command.BookingId).Select(r=>(Guid?)r.VehicleId).SingleOrDefaultAsync(ct);
  if(vehicleId==null)throw new NotFoundException("Reservation was not found.");
  await using var tx=await VehicleWriteTransaction.BeginAsync(db,vehicleId.Value,ct);
  var row=await db.VehicleReservations.SingleAsync(r=>r.BookingId==command.BookingId,ct);var at=RentalPeriod.Utc(command.ReturnedAt);
  if(row.ReturnedAt!=null)
  {
   if(row.HeldRentalId!=command.RentalId||row.ReturnOdometer!=command.Odometer||row.ReturnedAt!=at)throw new DuplicateException("Return command differs from the saved handover.");
   return new(row.BookingId,command.RentalId,true);
  }
  var vehicle=await db.Vehicles.SingleAsync(v=>v.Id==row.VehicleId,ct);
  if(!row.IsHeld||row.Released||row.HeldRentalId!=command.RentalId)throw new DuplicateException("Only this vehicle's active rental can record return.");
  if(row.PickupAt==null||at<row.PickupAt||command.Odometer<row.PickupOdometer||command.Odometer<vehicle.Mileage)throw new ValidationException("Return mileage and time cannot precede pickup or the vehicle's recorded mileage.");
  row.IsHeld=false;row.Released=true;row.ReleasedAt=at;row.ReturnedAt=at;row.ReturnOdometer=command.Odometer;
  vehicle.Mileage=command.Odometer;vehicle.UpdatedAt=DateTime.UtcNow;
  vehicle.Status=await MaintenanceAvailability.CurrentAsync(db,row.VehicleId,ct)?VehicleStatus.Maintenance:VehicleStatus.Available;
  await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return new(row.BookingId,command.RentalId,true);
 }
 public async Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct=default)
 {
  start=RentalPeriod.Utc(start);end=RentalPeriod.Utc(end);
  if(RentalPeriod.Error(start,end) is {} error)throw new ValidationException(error);
  var vehicle=await vehicles.GetVehicleByIdAsync(vehicleId)??throw new NotFoundException("Vehicle was not found.");
  if(vehicle.Status!=VehicleStatus.Available)throw new ValidationException($"Vehicle is currently in '{vehicle.Status}' status and is not available for rental booking.");
  return !await db.VehicleReservations.AnyAsync(r=>r.VehicleId==vehicleId&&!r.Released&&(r.IsHeld || r.StartDateTime<end&&r.EndDateTime>start),ct) &&
   !await MaintenanceAvailability.Blocking(db,vehicleId,start,end).AnyAsync(ct);
 }
 private static ReservationAcknowledgement Ack(VehicleReservation row)=>new(row.BookingId,row.Released,row.TotalCost,JsonSerializer.Deserialize<VehicleResponse>(row.VehicleSnapshot)!);
}
