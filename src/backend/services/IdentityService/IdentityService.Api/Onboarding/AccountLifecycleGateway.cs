using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;

namespace IdentityService.Api.Onboarding;

public sealed class AccountLifecycleGateway(IHttpClientFactory clients,IServiceTokenProvider tokens)
{
    public async Task<bool> Outstanding(Guid user,CancellationToken ct=default)
    {
        using var response=await Send("OnboardingRental",HttpMethod.Get,$"internal/account-obligations/{user}",null,ct);
        return (await response.Content.ReadFromJsonAsync<AccountObligations>(ct))?.HasOutstandingRentals??throw new OnboardingException(503,"Rental obligations could not be checked.");
    }
    public async Task Change(string client,Guid user,string action,Guid operation,CancellationToken ct=default)
    {using var response=await Send(client,HttpMethod.Post,$"internal/accounts/{user}/{action}",new(operation),ct);}
    private async Task<HttpResponseMessage> Send(string client,HttpMethod method,string path,AccountLifecycleCommand? body,CancellationToken ct)
    {
        try
        {
            using var request=new HttpRequestMessage(method,path);request.Headers.Authorization=new("Bearer",await tokens.GetTokenAsync(ct));
            if(body!=null)request.Content=JsonContent.Create(body);
            var response=await clients.CreateClient(client).SendAsync(request,ct);
            if(response.StatusCode==System.Net.HttpStatusCode.Conflict){response.Dispose();throw new OnboardingException(409,"Resolve future bookings, unfinished rentals or a pending account operation before continuing.");}
            if(!response.IsSuccessStatusCode){response.Dispose();throw new OnboardingException(503,"Account lifecycle dependencies are unavailable. Your account has not been changed.");}
            return response;
        }
        catch(Exception ex)when(ex is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException||ex is OperationCanceledException&&!ct.IsCancellationRequested)
        {throw new OnboardingException(503,"Account lifecycle dependencies are unavailable. Please try again later.");}
    }
}
