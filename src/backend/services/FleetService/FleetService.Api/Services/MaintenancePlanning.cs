using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Services;
public partial class MaintenanceService
{
    private static string Snapshot(MaintenanceRecord r) => JsonSerializer.Serialize(new { r.ScheduledDateTime, r.PlannedEndDateTime,
        r.AssignedStaffId, r.AssignedStaffName, r.ServiceInformation, r.Details, r.Cost, Status = r.Status.ToString(), r.CompletedAt });
    private void Audit(MaintenanceRecord record, Guid actor, string action, string? before) => _dbContext.MaintenanceAuditEntries.Add(new() {
        Id = Guid.NewGuid(), MaintenanceRecordId = record.Id, ActorId = actor == Guid.Empty ? record.CreatedByUserId : actor,
        OccurredAt = DateTime.UtcNow, Action = action, Before = before, After = Snapshot(record) });
    private static void Actor(Guid actor) { if (actor == Guid.Empty) throw new ValidationException("Authenticated staff identity is required."); }
    private async Task CheckWindow(Guid vehicleId, DateTime start, DateTime? end, Guid? exclude, CancellationToken ct)
    {
        if (start == default || start < DateTime.UtcNow.AddMinutes(-5) || end.HasValue && (end <= start || end.Value - start > TimeSpan.FromDays(365)))
            throw new ValidationException("Plan an end after the start, within 365 days. Start must be current or in the future.");
        var now = DateTime.UtcNow;
        var stop = end ?? DateTime.MaxValue;
        var conflicting = end.HasValue ? await MaintenanceAvailability.Blocking(_dbContext, vehicleId, start, stop, exclude).AnyAsync(ct) :
            await _dbContext.MaintenanceRecords.AnyAsync(r => r.VehicleId == vehicleId && r.Id != exclude && (r.Status == MaintenanceStatus.SCHEDULED || r.Status == MaintenanceStatus.IN_PROGRESS), ct);
        if (conflicting) throw new DuplicateException("This vehicle already has conflicting maintenance.");
        var bookings = _dbContext.RentalRouting?.Remote == true
            ? await _dbContext.VehicleReservations.AnyAsync(r => r.VehicleId == vehicleId && !r.Released && (r.IsHeld ||
                (end.HasValue ? r.StartDateTime < stop && r.EndDateTime > start : r.EndDateTime > now)), ct)
            : await _dbContext.Bookings.AnyAsync(b => b.VehicleId == vehicleId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                (end.HasValue ? b.StartDateTime < stop && b.EndDateTime > start : b.EndDateTime > now), ct);
        if (bookings) throw new DuplicateException("Maintenance conflicts with an existing booking or active rental.");
    }
    private async Task RefreshVehicle(MaintenanceRecord record, CancellationToken ct)
    {
        if (record.Vehicle != null && record.Vehicle.Status is VehicleStatus.Available or VehicleStatus.Maintenance)
        { record.Vehicle.Status = (record.Status == MaintenanceStatus.IN_PROGRESS || record.Status == MaintenanceStatus.SCHEDULED && !record.PlannedEndDateTime.HasValue ||
            await MaintenanceAvailability.CurrentAsyncExcept(_dbContext, record.VehicleId, record.Id, ct)) ? VehicleStatus.Maintenance : VehicleStatus.Available; record.Vehicle.UpdatedAt = DateTime.UtcNow; }
    }
    public Task<MaintenanceRecordResponse?> UpdateMaintenanceRecordAsync(Guid id, Guid actor, UpdateMaintenanceRecordRequest request, CancellationToken ct = default)
    { Actor(actor); return UpdateDetails(id, request, actor, ct); }
    public Task<MaintenanceRecordResponse?> UpdateMaintenanceStatusAsync(Guid id, Guid actor, MaintenanceStatus status, CancellationToken ct = default)
    { Actor(actor); return UpdateStatus(id, status, actor, ct); }
    public async Task<MaintenanceRecordResponse?> ScheduleAsync(Guid id, Guid actor, MaintenanceScheduleRequest request, CancellationToken ct = default)
    {
        Actor(actor);
        var vehicleId = await _dbContext.MaintenanceRecords.AsNoTracking().Where(r => r.Id == id).Select(r => (Guid?)r.VehicleId).SingleOrDefaultAsync(ct);
        if (vehicleId == null) return null;
        await using var tx = await VehicleWriteTransaction.BeginAsync(_dbContext, vehicleId.Value, ct);
        var record = await _dbContext.MaintenanceRecords.Include(r => r.Vehicle).SingleAsync(r => r.Id == id, ct);
        if (record.Status != MaintenanceStatus.SCHEDULED || record.Vehicle?.Status is VehicleStatus.InUse or VehicleStatus.Retired)
            throw new ValidationException("Only scheduled maintenance for a vehicle outside an active rental can be rescheduled.");
        var start = NormalizeUtc(request.ScheduledDateTime); var end = NormalizeUtc(request.PlannedEndDateTime);
        await CheckWindow(record.VehicleId, start, end, id, ct);
        var before = Snapshot(record); record.ScheduledDateTime = start; record.PlannedEndDateTime = end; record.UpdatedAt = DateTime.UtcNow;
        Audit(record, actor, "Rescheduled", before);
        // Query sees persisted pre-edit values; exclude this record when deciding physical status.
        if (record.Vehicle?.Status == VehicleStatus.Maintenance && !await MaintenanceAvailability.CurrentAsyncExcept(_dbContext, record.VehicleId, record.Id, ct)) record.Vehicle.Status = VehicleStatus.Available;
        await _dbContext.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); return MapRecord(record);
    }
    public async Task<MaintenanceRecordResponse?> AssignAsync(Guid id, Guid actor, MaintenanceAssignmentRequest request, CancellationToken ct = default)
    {
        Actor(actor); MaintenanceStaffAccount? account = null;
        if (request.AssignedStaffId.HasValue)
        {
            if (request.AssignedStaffId == Guid.Empty) throw new ValidationException("Choose an active Maintenance Staff account.");
            account = await (_staff ?? throw new MaintenanceStaffUnavailableException()).GetAsync(request.AssignedStaffId.Value, ct);
            if (account == null) throw new ValidationException("The selected account is not active Maintenance Staff.");
        }
        var vehicleId = await _dbContext.MaintenanceRecords.AsNoTracking().Where(r => r.Id == id).Select(r => (Guid?)r.VehicleId).SingleOrDefaultAsync(ct);
        if (vehicleId == null) return null;
        await using var tx = await VehicleWriteTransaction.BeginAsync(_dbContext, vehicleId.Value, ct);
        var record = await _dbContext.MaintenanceRecords.Include(r => r.Vehicle).SingleAsync(r => r.Id == id, ct);
        if (record.Status is MaintenanceStatus.COMPLETED or MaintenanceStatus.CANCELLED) throw new ValidationException("Completed or cancelled work cannot be reassigned.");
        var name = account == null ? null : string.IsNullOrWhiteSpace(account.FullName) ? account.Username : account.FullName;
        if (record.AssignedStaffId == account?.Id && record.AssignedStaffName == name) return MapRecord(record);
        var before = Snapshot(record); record.AssignedStaffId = account?.Id; record.AssignedStaffName = name; record.UpdatedAt = DateTime.UtcNow;
        Audit(record, actor, "Assigned", before);
        await _dbContext.SaveChangesAsync(ct); if (tx != null) await tx.CommitAsync(ct); return MapRecord(record);
    }
    public async Task<IReadOnlyList<MaintenanceAuditEntry>?> AuditAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _dbContext.MaintenanceRecords.AnyAsync(r => r.Id == id, ct)) return null;
        return await _dbContext.MaintenanceAuditEntries.AsNoTracking().Where(a => a.MaintenanceRecordId == id).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(ct);
    }
}
