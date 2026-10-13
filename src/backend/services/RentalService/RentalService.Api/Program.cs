using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceFoundation(new OwnedServiceDescriptor("RentalService", "fleetflow_rental", "RENTAL"));
builder.Services.AddOpenApi();
var app = builder.Build();
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "RentalService v1"));
app.MapServiceFoundation(ServiceScopes.RentalReportsRead);
app.Run();

namespace RentalService.Api { public sealed class RentalApplication { } }
