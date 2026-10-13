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
  if(await db.MaintenanceRecords.AnyAsync(r=>r.VehicleId==command.VehicleId&&(r.Status==MaintenanceStatus.SCHEDULED||r.Status==MaintenanceStatus.IN_PROGRESS),ct) ||
   await db.VehicleReservations.AnyAsync(r=>r.VehicleId==command.VehicleId&&!r.Released&&r.StartDateTime<end&&r.EndDateTime>start,ct))
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
  if(!row.Released){row.Released=true;row.ReleasedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
  if(tx!=null)await tx.CommitAsync(ct);return Ack(row);
 }
 public async Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct=default)
 {
  start=RentalPeriod.Utc(start);end=RentalPeriod.Utc(end);
  if(RentalPeriod.Error(start,end) is {} error)throw new ValidationException(error);
  var vehicle=await vehicles.GetVehicleByIdAsync(vehicleId)??throw new NotFoundException("Vehicle was not found.");
  if(vehicle.Status!=VehicleStatus.Available)throw new ValidationException($"Vehicle is currently in '{vehicle.Status}' status and is not available for rental booking.");
  return !await db.VehicleReservations.AnyAsync(r=>r.VehicleId==vehicleId&&!r.Released&&r.StartDateTime<end&&r.EndDateTime>start,ct) &&
   !await db.MaintenanceRecords.AnyAsync(r=>r.VehicleId==vehicleId&&(r.Status==MaintenanceStatus.SCHEDULED||r.Status==MaintenanceStatus.IN_PROGRESS),ct);
 }
 private static ReservationAcknowledgement Ack(VehicleReservation row)=>new(row.BookingId,row.Released,row.TotalCost,JsonSerializer.Deserialize<VehicleResponse>(row.VehicleSnapshot)!);
}
