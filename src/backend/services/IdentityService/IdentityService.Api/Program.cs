using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using Microsoft.AspNetCore.Identity;
using IdentityService.Api.Entities;
using IdentityService.Api.Middleware;
using IdentityService.Api.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<FleetFlow.ServiceDefaults.IAccountAccessReader, IdentityService.Api.Services.AccountAccessReader>();

// Add services to the container.
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(GetDatabaseConnectionString(builder.Configuration)));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddSingleton<IdentityService.Api.Onboarding.IdentityVault>();
builder.Services.AddScoped<IdentityService.Api.Onboarding.OnboardingService>();
builder.Services.AddScoped<IdentityService.Api.Onboarding.DriverVerificationService>();
builder.Services.AddScoped<IdentityService.Api.Onboarding.GoogleTokenReader>();
builder.Services.AddScoped<IdentityService.Api.Onboarding.GoogleAuthentication>();
builder.Services.AddHttpClient("GoogleKeys",client=>client.Timeout=TimeSpan.FromSeconds(5)).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
if (builder.Configuration.GetValue<bool>("ONBOARDING_ENABLED"))
{
    new IdentityService.Api.Onboarding.IdentityVault(builder.Configuration).Validate();
    IdentityService.Api.Onboarding.OnboardingConfiguration.Validate(builder.Configuration,builder.Environment);
    FleetFlow.ServiceDefaults.ServiceAuthentication.AddServiceTokenClient(builder.Services,builder.Configuration,builder.Environment,"identity-service");
    var endpoint=FleetFlow.ServiceDefaults.InternalEndpoint.Read(builder.Configuration["INTERNAL_NOTIFICATION_URL"]??"http://127.0.0.1:5004/","notification-service",builder.Environment);
    builder.Services.AddHttpClient("OnboardingNotification",client=>{client.BaseAddress=endpoint;client.Timeout=TimeSpan.FromSeconds(5);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
    builder.Services.AddHostedService<IdentityService.Api.Onboarding.IdentityEmailWorker>();
    var rentalEndpoint=FleetFlow.ServiceDefaults.InternalEndpoint.Read(builder.Configuration["INTERNAL_RENTAL_URL"]??"http://127.0.0.1:5003/","rental-service",builder.Environment);
    builder.Services.AddHttpClient("OnboardingRental",client=>{client.BaseAddress=rentalEndpoint;client.Timeout=TimeSpan.FromSeconds(5);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
    builder.Services.AddScoped<IdentityService.Api.Onboarding.AccountLifecycleGateway>();
    builder.Services.AddHostedService<IdentityService.Api.Onboarding.AccountLifecycleWorker>();
}
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IProfileImageService, ProfileImageService>();

// Configure Apache Kafka Messaging
builder.Services.Configure<IdentityService.Api.Messaging.KafkaSettings>(builder.Configuration.GetSection(IdentityService.Api.Messaging.KafkaSettings.SectionName));
builder.Services.AddSingleton<IdentityService.Api.Messaging.IKafkaProducerService, IdentityService.Api.Messaging.KafkaProducerService>();
builder.Services.AddHostedService<IdentityService.Api.Messaging.UserEventConsumerService>();

// Configure JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = IdentityService.Api.Security.JwtSigningKey.Resolve(builder.Configuration, builder.Environment);
FleetFlow.ServiceDefaults.ServiceAuthentication.AddInternalServiceAuthentication(builder.Services, builder.Configuration, jwtKey);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("onboarding", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("service-token", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256]
    };
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var rawAllowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) 
                     ?? new[] { "https://fleetflow-frontend.azurewebsites.net", "http://localhost:5173", "http://localhost:5174", "http://127.0.0.1:5173", "http://127.0.0.1:5174", "http://localhost:3000" };
var allowedOrigins = rawAllowedOrigins.Select(o => o.TrimEnd('/')).Distinct().ToArray();

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
app.UseMiddleware<FleetFlow.ServiceDefaults.ApiErrorsMiddleware>();

var isDbReady = false;

// Non-blocking background database initialization and seeding
_ = Task.Run(async () =>
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            await IdentityService.Api.Onboarding.OnboardingSchema.EnsureAsync(dbContext,scope.ServiceProvider.GetRequiredService<IdentityService.Api.Onboarding.IdentityVault>());
            await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"AccessVersion\" bigint NOT NULL DEFAULT 0");

            // Idempotent Role Seeding
            var defaultRoles = new[] { "CUSTOMER", "FLEET_MANAGER", "MAINTENANCE_STAFF", "ADMIN" };
            bool hasChanges = false;
            foreach (var roleName in defaultRoles)
            {
                var roleExists = await dbContext.Roles.AnyAsync(r => r.Name.ToUpper() == roleName.ToUpper());
                if (!roleExists)
                {
                    dbContext.Roles.Add(new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName
                    });
                    hasChanges = true;
                }
            }
            if (hasChanges)
            {
                await dbContext.SaveChangesAsync();
            }

            // Ensure new User columns exist on pre-existing database
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""FullName"" character varying(100) NOT NULL DEFAULT '';
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""PhoneNumber"" character varying(20) NOT NULL DEFAULT '';
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Address"" character varying(250) NOT NULL DEFAULT '';
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DrivingLicenseNumber"" character varying(50) NOT NULL DEFAULT '';
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ProfileImageUrl"" character varying(500) NULL;
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsActive"" boolean NOT NULL DEFAULT TRUE;
                    ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""MustChangePassword"" boolean NOT NULL DEFAULT FALSE;
                ");

                // Seed new local demo accounts only; never overwrite existing credentials or roles.
                if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("QA_SEED_ENABLED"))
                {
                    var seedPassword = builder.Configuration["QA_SEED_PASSWORD"];
                    if (string.IsNullOrWhiteSpace(seedPassword) && !builder.Environment.IsDevelopment())
                        throw new InvalidOperationException("Explicit QA seeding requires a configured password.");
                    await QaAccountSeeder.SeedAsync(dbContext, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), seedPassword ?? "Password@123");
                }
                isDbReady = true;
                Console.WriteLine("[Database] IdentityService database initialized and ready.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup Warning] Column check/seeding: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Warning] Database initialization deferred: {ex.Message}");
    }
});


// Configure the HTTP request pipeline.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
    options.RoutePrefix = "swagger";
});

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
{
    app.UseHttpsRedirection();
}

// Static file serving for uploads (/uploads/profiles/...)
var uploadRoot = builder.Configuration["IDENTITY_UPLOAD_ROOT"];
if (string.IsNullOrWhiteSpace(uploadRoot))
{
    uploadRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
}
Directory.CreateDirectory(Path.Combine(uploadRoot, "profiles"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads"
});

app.UseRouting();
app.UseCors();
app.UseRateLimiter();


app.UseMiddleware<IdentityService.Api.Middleware.AccountChangeConcurrencyMiddleware>();
app.UseAuthentication();
FleetFlow.ServiceDefaults.AccountAccessRegistration.UseAccountAccess(app);
app.UseAuthorization();

// Intercept and block access to protected application resources when user is flagged to change password
app.UseMiddleware<ForcedPasswordChangeMiddleware>();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (IdentityDbContext dbContext) =>
{
    try
    {
        var ready = isDbReady && await dbContext.Database.CanConnectAsync();
        return Results.Json(new { status = ready ? "Ready" : "NotReady", ready }, statusCode: ready ? 200 : 503);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "NotReady", ready = false }, statusCode: 503);
    }
}).AllowAnonymous();

app.MapGet("/health", async (IdentityDbContext dbContext) =>
{
    try
    {
        var ready = isDbReady && await dbContext.Database.CanConnectAsync();
        return Results.Json(new { status = ready ? "Healthy" : "NotReady", ready }, statusCode: ready ? 200 : 503);
    }
    catch (Exception) { return Results.Json(new { status = "NotReady", ready = false }, statusCode: 503); }
}).AllowAnonymous();

app.Run();

static string GetDatabaseConnectionString(IConfiguration configuration)
{
    var connStr = configuration.GetConnectionString("DefaultConnection") 
                  ?? configuration["ConnectionStrings__DefaultConnection"];
    if (!string.IsNullOrEmpty(connStr))
    {
        if (!connStr.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
        {
            connStr = connStr.TrimEnd(';') + ";Trust Server Certificate=true;";
        }
        if (!connStr.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            connStr = connStr.TrimEnd(';') + ";Timeout=10;Command Timeout=30;";
        }
        return connStr;
    }

    var host = configuration["DB_HOST"] ?? "localhost";
    var port = configuration["DB_PORT"] ?? "5432";
    var user = configuration["DB_USER"] ?? "postgres";
    var password = configuration["DB_PASSWORD"] ?? "your_password_here";
    var dbName = configuration["AUTH_DB_NAME"] ?? "fleetflow_auth";

    var isExplicitSsl = string.Equals(configuration["DB_SSL"], "true", StringComparison.OrdinalIgnoreCase);
    var isRemoteHost = !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !host.Equals("127.0.0.1");
    var requiresSsl = isExplicitSsl || (isRemoteHost && (host.Contains("azure.com", StringComparison.OrdinalIgnoreCase) || host.Contains("postgres", StringComparison.OrdinalIgnoreCase) || host.Contains("database", StringComparison.OrdinalIgnoreCase)));

    var sslMode = requiresSsl ? ";Ssl Mode=Require;Trust Server Certificate=true" : "";
    return $"Host={host};Port={port};Database={dbName};Username={user};Password={password}{sslMode};Timeout=10;Command Timeout=30;";
}

namespace IdentityService.Api { public sealed class IdentityApplication { } }
