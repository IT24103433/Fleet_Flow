using System.Globalization;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;

namespace RentalService.Api.Services;

public sealed class DriverEligibilityClient(IHttpClientFactory clients,IServiceTokenProvider tokens,IConfiguration configuration)
{
    public bool Enabled=>configuration.GetValue<bool>("REQUIRE_DRIVER_VERIFICATION");
    public async Task Check(Guid customer,DateTime returnAt,CancellationToken ct)
    {
        if(!configuration.GetValue<bool>("REQUIRE_DRIVER_VERIFICATION"))return;
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,$"internal/driver-eligibility/{customer}?returnAt={Uri.EscapeDataString(returnAt.ToString("O",CultureInfo.InvariantCulture))}");
            request.Headers.Authorization=new("Bearer",await tokens.GetTokenAsync(ct));
            using var response=await clients.CreateClient("DriverIdentity").SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)throw new BookingRequestException(503,"Driver verification is temporarily unavailable. Try again later.");
            var status=await response.Content.ReadFromJsonAsync<DriverEligibility>(ct);
            if(status==null||!status.Enabled)throw new BookingRequestException(503,"The booking verification policy is not activated consistently.");
            if(!status.Eligible)throw new BookingRequestException(409,status.Reason??"Complete driver verification before making a new booking.");
        }
        catch(Exception ex) when(ex is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException||ex is OperationCanceledException&&!ct.IsCancellationRequested)
        {throw new BookingRequestException(503,"Driver verification is temporarily unavailable. Try again later.");}
    }
}
