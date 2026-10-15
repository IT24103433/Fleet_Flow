using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationService.Api.Data;
using NotificationService.Api.Messaging;
using NotificationService.Api.Services;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options=>options.AddPolicy("support",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=5,Window=TimeSpan.FromHours(1),QueueLimit=0,AutoReplenishment=true})));
builder.Services.AddSingleton<NotificationService.Api.Email.EmailVault>();
builder.Services.AddScoped<NotificationService.Api.Email.EmailStore>();
if(builder.Configuration.GetValue<bool>("EMAIL_ENABLED"))
{
    new NotificationService.Api.Email.EmailVault(builder.Configuration).Validate();
    builder.Services.AddHttpClient("EmailIdentity",client=>{client.BaseAddress=ServiceEndpoints.Identity(builder.Configuration,builder.Environment);client.Timeout=TimeSpan.FromSeconds(5);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
    builder.Services.AddHostedService<NotificationService.Api.Email.EmailWorker>();
}
builder.AddServiceFoundation(new OwnedServiceDescriptor("NotificationService","fleetflow_notifications","NOTIFICATION"));
var activation=NotificationActivation.Read(builder.Configuration);
builder.Services.AddSingleton(activation);
builder.Services.AddDbContext<NotificationDbContext>((sp,options)=>options.UseNpgsql(sp.GetRequiredService<OwnedDatabase>().ConnectionString));
builder.Services.AddScoped<InboxService>();builder.Services.AddScoped<NotificationEventProcessor>();
builder.Services.AddScoped<NotificationActivationFilter>();
builder.Services.RemoveAll<IDatabaseReadiness>();builder.Services.AddSingleton<IDatabaseReadiness,NotificationReadiness>();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));builder.Services.AddHostedService<NotificationKafkaConsumer>();
builder.Services.AddControllers();builder.Services.AddOpenApi();
var app=builder.Build();app.UseMiddleware<FleetFlow.ServiceDefaults.ApiErrorsMiddleware>();app.UseRateLimiter();app.MapOpenApi();app.UseSwaggerUI(o=>o.SwaggerEndpoint("/openapi/v1.json","NotificationService v1"));
app.MapServiceFoundation(ServiceScopes.NotificationOperations,activation.Active,2);
app.UseMiddleware<NotificationService.Api.Middleware.ForcedPasswordChangeMiddleware>();app.MapControllers();app.Run();
namespace NotificationService.Api { public sealed class NotificationApplication {} }
