using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Entities;

namespace IdentityService.Api.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<IdentityService.Api.Onboarding.IdentityOperation> Operations => Set<IdentityService.Api.Onboarding.IdentityOperation>();
    public DbSet<IdentityService.Api.Onboarding.DocumentClaim> DocumentClaims => Set<IdentityService.Api.Onboarding.DocumentClaim>();
    public DbSet<IdentityService.Api.Onboarding.ExternalAccount> ExternalAccounts => Set<IdentityService.Api.Onboarding.ExternalAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IdentityService.Api.Onboarding.IdentityOperation>(e => {
            e.ToTable("IdentityOperations");e.HasKey(x=>x.Id);e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasIndex(x=>new{x.UserId,x.Kind});e.HasIndex(x=>new{x.Kind,x.State,x.NextAttemptAt});
        });
        modelBuilder.Entity<IdentityService.Api.Onboarding.DocumentClaim>(e => {
            e.ToTable("DocumentClaims");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.Kind,x.Fingerprint}).IsUnique();
        });
        modelBuilder.Entity<IdentityService.Api.Onboarding.ExternalAccount>(e => {
            e.ToTable("ExternalAccounts");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.Provider,x.Subject}).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.AccessVersion).IsConcurrencyToken().HasDefaultValue(0L);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).HasDefaultValue(string.Empty);
            entity.Property(e => e.Address).HasMaxLength(250).HasDefaultValue(string.Empty);
            entity.Property(e => e.DrivingLicenseNumber).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
        });


        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Many-to-many relationship user <-> role
        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserRoles",
                j => j.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("UserRoles");
                    j.HasKey("UserId", "RoleId");
                });
    }
}
