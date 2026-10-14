using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
namespace FleetService.Api.Services;
public sealed class MaintenanceStaffUnavailableException : Exception;
public interface IMaintenanceStaffDirectory
{
    Task<IReadOnlyList<MaintenanceStaffAccount>> ListAsync(CancellationToken ct);
    Task<MaintenanceStaffAccount?> GetAsync(Guid id, CancellationToken ct);
}
public sealed class MaintenanceStaffDirectory(HttpClient http, IServiceTokenProvider tokens) : IMaintenanceStaffDirectory
{
    private async Task<T?> Send<T>(string path, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return default;
            if (!response.IsSuccessStatusCode) throw new MaintenanceStaffUnavailableException();
            return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new MaintenanceStaffUnavailableException();
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new MaintenanceStaffUnavailableException(); }
    }
    public async Task<IReadOnlyList<MaintenanceStaffAccount>> ListAsync(CancellationToken ct)
    {
        var accounts = await Send<List<MaintenanceStaffAccount>>("internal/staff/maintenance", ct) ?? throw new MaintenanceStaffUnavailableException();
        if (accounts.Count > 1000 || accounts.Any(a => a.Id == Guid.Empty || string.IsNullOrWhiteSpace(a.Username)) || accounts.Select(a => a.Id).Distinct().Count() != accounts.Count) throw new MaintenanceStaffUnavailableException();
        return accounts;
    }
    public async Task<MaintenanceStaffAccount?> GetAsync(Guid id, CancellationToken ct)
    {
        var account = await Send<MaintenanceStaffAccount>($"internal/staff/maintenance/{id}", ct);
        if (account != null && (account.Id != id || string.IsNullOrWhiteSpace(account.Username))) throw new MaintenanceStaffUnavailableException();
        return account;
    }
}
