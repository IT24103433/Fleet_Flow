using FleetFlow.ServiceDefaults;
namespace FleetService.Api.Messaging;
public sealed record NotificationRouting(bool Remote,Uri BaseUrl)
{
    public static NotificationRouting Read(IConfiguration config,IHostEnvironment environment)
    {
        var mode=config["NOTIFICATION_OWNERSHIP"]??"Legacy";
        if(mode is not ("Legacy" or "Remote")) throw new InvalidOperationException("NOTIFICATION_OWNERSHIP must be Legacy or Remote.");
        var raw=config["INTERNAL_NOTIFICATION_URL"]??"http://localhost:5004/";
        if(!Uri.TryCreate(raw.TrimEnd('/')+"/",UriKind.Absolute,out var url) || !string.IsNullOrEmpty(url.UserInfo) ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || url.AbsolutePath!="/" ||
            (url.Scheme!="https" && !(url.Scheme=="http" && environment.IsDevelopment() && (url.IsLoopback || url.Host=="notification-service"))))
            throw new InvalidOperationException("Configure a trusted HTTPS Notification URL; HTTP is restricted to local Development.");
        if(mode=="Remote" && !(config.GetValue<bool?>("SERVICE_AUTH_ENABLED")??config.GetValue<bool>("ServiceAuth:Enabled")))
            throw new InvalidOperationException("Remote notification ownership requires internal service authentication.");
        return new(mode=="Remote",url);
    }
}
