using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FleetService.Api.Data;

namespace FleetService.Tests;

public class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
{
    private readonly string _databaseName = "FleetTestDb_" + Guid.NewGuid();
    private const string JwtKey = "TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError";
    private const string Issuer = "FleetFlow.IdentityService";
    private const string Audience = "FleetFlow.Client";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:Key", JwtKey);

        builder.ConfigureServices(services =>
        {
            var efDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<FleetDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(FleetDbContext) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ServiceType.FullName?.Contains("Npgsql") == true).ToList();

            foreach (var descriptor in efDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<FleetDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }
}
