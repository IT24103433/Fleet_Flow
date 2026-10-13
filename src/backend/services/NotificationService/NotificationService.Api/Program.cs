using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceFoundation(new OwnedServiceDescriptor("NotificationService", "fleetflow_notifications", "NOTIFICATION"));
builder.Services.AddOpenApi();
var app = builder.Build();
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "NotificationService v1"));
app.MapServiceFoundation(ServiceScopes.NotificationOperations);
app.Run();

namespace NotificationService.Api { public sealed class NotificationApplication { } }
