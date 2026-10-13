using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
namespace RentalService.Api.Data;
public sealed class RentalBooking
{
 public Guid Id {get;set;}
 public Guid CustomerId {get;set;}
 public Guid VehicleId {get;set;}
 public DateTime StartDateTime {get;set;}
 public DateTime EndDateTime {get;set;}
 public BookingStatus Status {get;set;}
 public decimal TotalCost {get;set;}
 public DateTime CreatedAt {get;set;}
 public DateTime? UpdatedAt {get;set;}
 public string VehicleSnapshot {get;set;}="";
}
public sealed class BookingWorkflow
{
 public Guid Id {get;set;}
 public Guid CustomerId {get;set;}
 public Guid RequestKey {get;set;}
 public Guid VehicleId {get;set;}
 public DateTime StartDateTime {get;set;}
 public DateTime EndDateTime {get;set;}
 public string Phase {get;set;}="ReserveRequested";
 public DateTime CreatedAt {get;set;}
 public int Attempts {get;set;}
 public DateTime NextAttemptAt {get;set;}
 public int? FailureStatus {get;set;}
 public string? FailureMessage {get;set;}
}
public sealed class RentalDbContext(DbContextOptions<RentalDbContext> options):DbContext(options)
{
 public DbSet<RentalBooking> Bookings=>Set<RentalBooking>();
 public DbSet<BookingWorkflow> Workflows=>Set<BookingWorkflow>();
 public DbSet<NotificationOutbox> NotificationOutbox=>Set<NotificationOutbox>();
 protected override void OnModelCreating(ModelBuilder model)
 {
  model.Entity<RentalBooking>(e=>{e.ToTable("Bookings");e.HasKey(b=>b.Id);e.Property(b=>b.Status).HasConversion<string>();e.Property(b=>b.TotalCost).HasPrecision(18,2);e.HasIndex(b=>b.CustomerId);});
  model.Entity<BookingWorkflow>(e=>{e.ToTable("BookingWorkflows");e.HasKey(w=>w.Id);e.HasIndex(w=>new{w.CustomerId,w.RequestKey}).IsUnique();e.HasIndex(w=>new{w.Phase,w.NextAttemptAt});});
  model.Entity<NotificationOutbox>(e=>{e.ToTable("NotificationOutbox");e.HasKey(o=>o.Id);e.Property(o=>o.EventType).HasMaxLength(100);e.Property(o=>o.Status).HasMaxLength(20);e.Property(o=>o.LastErrorCode).HasMaxLength(50);e.HasIndex(o=>new{o.Status,o.NextAttemptAt});});
 }
}
