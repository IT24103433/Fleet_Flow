using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<FleetDbContext>(options =>
    options.UseNpgsql(GetDatabaseConnectionString(builder.Configuration)));

builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleImageService, VehicleImageService>();

// Configure JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? builder.Configuration["Jwt__Key"] ?? "FleetFlowSuperSecretSecurityKey2026!#ForJWTTokenGeneration";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',') 
                     ?? new[] { "https://fleetflow-frontend.azurewebsites.net", "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Startup Database Creation and Category Seeding
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Idempotent Default Category Seeding
        var defaultCategories = new (string Name, string Description)[]
        {
            ("Executive Sedan", "Luxury and executive passenger sedans for corporate and premium mobility."),
            ("Full-Size SUV", "Spacious premium sport utility vehicles with high capacity and all-weather capability."),
            ("Commercial Cargo", "Heavy-duty cargo vans and commercial transport vehicles for logistics."),
            ("Compact EV", "High-efficiency 100% electric urban compact vehicles."),
            ("Premium Coupe", "High-performance premium sport and luxury coupes.")
        };

        bool hasChanges = false;
        foreach (var (name, description) in defaultCategories)
        {
            var exists = await dbContext.VehicleCategories.AnyAsync(c => c.Name.ToUpper() == name.ToUpper());
            if (!exists)
            {
                dbContext.VehicleCategories.Add(new VehicleCategory
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description
                });
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }

        // Ensure VehicleImages table exists on pre-existing database
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""VehicleImages"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""VehicleId"" uuid NOT NULL,
                    ""FileName"" character varying(255) NOT NULL,
                    ""OriginalFileName"" character varying(255) NOT NULL,
                    ""ContentType"" character varying(100) NOT NULL,
                    ""FileSize"" bigint NOT NULL,
                    ""RelativeUrl"" character varying(500) NOT NULL,
                    ""Caption"" character varying(255) NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""FK_VehicleImages_Vehicles_VehicleId"" FOREIGN KEY (""VehicleId"") REFERENCES ""Vehicles"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_VehicleImages_VehicleId"" ON ""VehicleImages"" (""VehicleId"");
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup Warning] Table check: {ex.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup Warning] Database initialization deferred: {ex.Message}");
}

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Static file serving for uploads (/uploads/vehicles/...)
var uploadRoot = builder.Configuration["FLEET_UPLOAD_ROOT"];
if (string.IsNullOrWhiteSpace(uploadRoot))
{
    uploadRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
}
Directory.CreateDirectory(Path.Combine(uploadRoot, "vehicles"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads"
});

app.UseCors();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (FleetDbContext dbContext) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        return canConnect 
            ? Results.Ok(new { status = "Healthy", database = "Connected" })
            : Results.Problem("Database connection failed");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

app.Run();

static string GetDatabaseConnectionString(IConfiguration configuration)
{
    var connStr = configuration.GetConnectionString("DefaultConnection") 
                  ?? configuration["ConnectionStrings__DefaultConnection"];
    if (!string.IsNullOrEmpty(connStr))
    {
        return connStr;
    }

    var host = configuration["DB_HOST"] ?? "localhost";
    var port = configuration["DB_PORT"] ?? "5432";
    var user = configuration["DB_USER"] ?? "postgres";
    var password = configuration["DB_PASSWORD"] ?? "your_password_here";
    var dbName = configuration["FLEET_DB_NAME"] ?? "fleetflow_fleet";

    var sslMode = host.Contains("azure.com") ? ";Ssl Mode=Require" : "";
    return $"Host={host};Port={port};Database={dbName};Username={user};Password={password}{sslMode};";
}
