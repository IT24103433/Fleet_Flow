using Microsoft.EntityFrameworkCore;
using FleetService.Api.Entities;

namespace FleetService.Api.Data;

public class FleetDbContext : DbContext
{
    public FleetDbContext(DbContextOptions<FleetDbContext> options) : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();
    public DbSet<VehicleImage> VehicleImages => Set<VehicleImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehicleCategory>(entity =>
        {
            entity.ToTable("VehicleCategories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Vin).IsRequired().HasMaxLength(17);
            entity.HasIndex(e => e.Vin).IsUnique();

            entity.Property(e => e.LicensePlate).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.LicensePlate).IsUnique();

            entity.Property(e => e.Make).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Year).IsRequired();
            entity.Property(e => e.DailyRate).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Transmission).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FuelType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SeatingCapacity).IsRequired().HasMaxLength(50);
            entity.Property(e => e.HubLocation).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Mileage).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);

            entity.HasOne(v => v.Category)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(v => v.VehicleCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VehicleImage>(entity =>
        {
            entity.ToTable("VehicleImages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FileSize).IsRequired();
            entity.Property(e => e.RelativeUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Caption).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(vi => vi.Vehicle)
                .WithMany(v => v.Images)
                .HasForeignKey(vi => vi.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
