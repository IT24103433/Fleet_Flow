using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using IdentityService.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace IdentityService.Api.Services;
public sealed class AccountAccessReader(IdentityDbContext db) : IAccountAccessReader
{
    public async Task<AccountAccessState?> ReadAsync(Guid userId, CancellationToken ct)
    {
        try { return await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new AccountAccessState(u.Id, u.AccessVersion, u.IsActive, u.MustChangePassword)).SingleOrDefaultAsync(ct); }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new AccountAccessUnavailableException(); }
    }
}
