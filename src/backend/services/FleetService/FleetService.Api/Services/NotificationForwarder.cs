using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Messaging;
namespace FleetService.Api.Services;
public sealed class NotificationForwarder(NotificationRouting routing,IHttpClientFactory clients)
{
    public bool Enabled=>routing.Remote;
    public async Task<IActionResult> ForwardAsync(HttpRequest request,string path,CancellationToken ct)
    {
        using var message=new HttpRequestMessage(new HttpMethod(request.Method),"api/notifications"+path);
        message.Headers.TryAddWithoutValidation("Authorization",request.Headers.Authorization.ToString());
        if(request.Method=="POST")
        {
            // MVC has already validated the user/role. Serialize the bound DTO in the controller instead.
            throw new InvalidOperationException("Use the validated request body overload.");
        }
        return await SendAsync(message,ct);
    }
    public async Task<IActionResult> CreateAsync(HttpRequest request,object body,CancellationToken ct)
    {
        using var message=new HttpRequestMessage(HttpMethod.Post,"api/notifications/system")
        { Content=System.Net.Http.Json.JsonContent.Create(body) };
        message.Headers.TryAddWithoutValidation("Authorization",request.Headers.Authorization.ToString());
        return await SendAsync(message,ct);
    }
    private async Task<IActionResult> SendAsync(HttpRequestMessage message,CancellationToken ct)
    {
        try
        {
            using var response=await clients.CreateClient("NotificationService").SendAsync(message,ct);
            if(response.StatusCode==System.Net.HttpStatusCode.NoContent) return new NoContentResult();
            return new ContentResult { StatusCode=(int)response.StatusCode,Content=await response.Content.ReadAsStringAsync(ct),
                ContentType=response.Content.Headers.ContentType?.ToString()??"application/json" };
        }
        catch(Exception ex) when(ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        { return new ObjectResult(new { message="Notifications are unavailable. Please try again." }){StatusCode=503}; }
    }
}
