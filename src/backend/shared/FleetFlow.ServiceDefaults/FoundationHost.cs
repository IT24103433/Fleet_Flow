using System.Text;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FleetFlow.ServiceDefaults;

public static class FoundationHost
{
    public static void AddServiceFoundation(this WebApplicationBuilder builder, OwnedServiceDescriptor service)
    {
        var key = ServiceAuthentication.UserSigningKey(builder.Configuration, builder.Environment);
        var database = OwnedDatabase.Read(builder.Configuration, service);
        builder.Services.AddSingleton(database);
        builder.Services.AddSingleton<IDatabaseReadiness, DatabaseReadiness>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true, RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddInternalServiceAuthentication(builder.Configuration, key);
        builder.Services.AddServiceTokenClient(builder.Configuration, builder.Environment,
            service.Name == "RentalService" ? "rental-service" : "notification-service");
        builder.Services.AddRemoteAccountAccess(builder.Configuration, builder.Environment);
        var origins = (builder.Configuration["ALLOWED_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins)
            .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Total-Count","X-Page","X-Page-Size","X-Total-Pages")));
    }

    public static void MapServiceFoundation(this WebApplication app, string internalScope, bool businessActivated = false, int schemaVersion = 1)
    {
        var descriptor = app.Services.GetRequiredService<OwnedDatabase>().Service;
        var information = new ServiceInformation(descriptor.Name, descriptor.Database, schemaVersion, businessActivated);
        app.UseCors();
        app.UseAuthentication();
        app.UseAccountAccess();
        app.UseAuthorization();
        app.MapGet("/health/live", () => Results.Ok(new { status = "Alive", service = descriptor.Name }));
        async Task<IResult> Ready(IDatabaseReadiness database, CancellationToken cancellationToken)
        {
            var ready = await database.IsReadyAsync(cancellationToken);
            return Results.Json(new { status = ready ? "Ready" : "NotReady", service = descriptor.Name,
                database = descriptor.Database, schemaVersion = ready ? schemaVersion : 0, businessActivated },
                statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        }
        app.MapGet("/health/ready", Ready);
        app.MapGet("/health", Ready);
        app.MapGet("/api/service", () => information).RequireAuthorization();
        app.MapGet("/internal/service", () => information).RequireAuthorization(internalScope);
    }
}
