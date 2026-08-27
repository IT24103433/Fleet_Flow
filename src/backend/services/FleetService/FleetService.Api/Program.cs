using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<FleetDbContext>(options =>
    options.UseNpgsql(GetDatabaseConnectionString(builder.Configuration)));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetDatabaseConnectionString(IConfiguration configuration)
{
    var host = configuration["DB_HOST"] ?? "localhost";
    var port = configuration["DB_PORT"] ?? "5432";
    var user = configuration["DB_USER"] ?? "postgres";
    var password = configuration["DB_PASSWORD"] ?? "your_password_here";
    var dbName = configuration["FLEET_DB_NAME"] ?? "fleetflow_fleet";

    return $"Host={host};Port={port};Database={dbName};Username={user};Password={password};";
}
