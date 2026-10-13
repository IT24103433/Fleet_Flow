using Microsoft.Extensions.Hosting;
namespace FleetFlow.ServiceDefaults;
public static class InternalEndpoint
{
 public static Uri Read(string raw,string localService,IHostEnvironment environment)
 {
  if(!Uri.TryCreate(raw.TrimEnd('/')+"/",UriKind.Absolute,out var url)||url.UserInfo!=""||url.Query!=""||url.Fragment!=""||url.AbsolutePath!="/"||
   (url.Scheme!="https" && !(url.Scheme=="http"&&environment.IsDevelopment()&&(url.IsLoopback||url.Host==localService))))
   throw new InvalidOperationException("Configure a trusted HTTPS service URL; HTTP is restricted to local Development.");
  return url;
 }
}
