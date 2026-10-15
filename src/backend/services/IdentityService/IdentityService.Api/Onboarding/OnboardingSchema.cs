using IdentityService.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public static class OnboardingSchema
{
    public static async Task EnsureAsync(IdentityDbContext db,IdentityVault vault)
    {
        if(!db.Database.IsNpgsql())return;
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailVerificationMethod" text NOT NULL DEFAULT 'Unverified';
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "NeedsInitialEmailOtp" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailSecondStepEnabled" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "HasLocalPassword" boolean NOT NULL DEFAULT true;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
            """);
        if(!vault.Enabled)return;
        vault.Validate();
        await using var transaction=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            SELECT pg_advisory_xact_lock(74211001);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_NormalizedUsername" ON "Users" (lower(trim("Username")));
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_NormalizedEmail" ON "Users" (lower(trim("Email")));
            CREATE TABLE IF NOT EXISTS "IdentityOperations" (
                "Id" uuid PRIMARY KEY, "UserId" uuid NULL, "Kind" text NOT NULL, "State" text NOT NULL,
                "Ciphertext" text NOT NULL, "CodeHash" text NOT NULL, "AddressHash" text NOT NULL,
                "Attempts" integer NOT NULL, "CreatedAt" timestamp with time zone NOT NULL,
                "ExpiresAt" timestamp with time zone NULL, "NextAttemptAt" timestamp with time zone NULL, "Version" uuid NOT NULL);
            CREATE INDEX IF NOT EXISTS "IX_IdentityOperations_UserId_Kind" ON "IdentityOperations" ("UserId","Kind");
            CREATE INDEX IF NOT EXISTS "IX_IdentityOperations_Kind_State_NextAttemptAt" ON "IdentityOperations" ("Kind","State","NextAttemptAt");
            CREATE TABLE IF NOT EXISTS "DocumentClaims" (
                "Id" uuid PRIMARY KEY,"UserId" uuid NOT NULL,"Kind" text NOT NULL,"Fingerprint" text NOT NULL,"RevisionId" uuid NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentClaims_Kind_Fingerprint" ON "DocumentClaims" ("Kind","Fingerprint");
            CREATE TABLE IF NOT EXISTS "ExternalAccounts" (
                "Id" uuid PRIMARY KEY,"UserId" uuid NOT NULL,"Provider" text NOT NULL,"Subject" text NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExternalAccounts_Provider_Subject" ON "ExternalAccounts" ("Provider","Subject");
            """);
        // A durable marker makes the explicitly approved legacy-admin waiver run once.
        var keyMarker=await db.Operations.SingleOrDefaultAsync(x=>x.Kind=="CryptoConfiguration");
        var lookupFingerprint=vault.Fingerprint("key-validation","FleetFlow onboarding key configuration");
        if(keyMarker==null)db.Operations.Add(new(){Kind="CryptoConfiguration",State="Installed",Ciphertext=vault.Encrypt(lookupFingerprint,"KeyMarker")});
        else if(!vault.Matches(vault.Decrypt<string>(keyMarker.Ciphertext,"KeyMarker"),lookupFingerprint))
            throw new InvalidOperationException("Onboarding keys changed without a reviewed key migration. Preserve the configured data and lookup keys.");
        if(!await db.Operations.AnyAsync(x=>x.Kind=="Bootstrap"))
        {
            var admins=await db.Users.Include(x=>x.Roles).Where(x=>x.IsActive&&x.DeletedAt==null&&x.Roles.Any(r=>r.Name=="ADMIN")).ToListAsync();
            foreach(var admin in admins)
            {
                admin.EmailVerificationMethod="AdminWaived";admin.NeedsInitialEmailOtp=false;
                db.Operations.Add(new(){UserId=admin.Id,Kind="Audit",State="InitialAdminWaiver",Ciphertext=vault.Encrypt(new{reason="Existing administrator access retained at reviewed onboarding activation."},"audit")});
            }
            db.Operations.Add(new(){Kind="Bootstrap",State="Completed"});await db.SaveChangesAsync();
        }
        await db.SaveChangesAsync();await transaction.CommitAsync();
    }
}
