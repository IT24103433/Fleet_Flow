using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using Microsoft.AspNetCore.Identity;
using IdentityService.Api.Entities;
using IdentityService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(GetDatabaseConnectionString(builder.Configuration)));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IProfileImageService, ProfileImageService>();


// Configure JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? builder.Configuration["Jwt__Key"] ?? "FleetFlowSuperSecretSecurityKey2026!#ForJWTTokenGeneration";

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
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',') 
                     ?? new[] { "https://fleetflow-frontend.azurewebsites.net", "http://localhost:5173", "http://localhost:5174", "http://127.0.0.1:5173", "http://127.0.0.1:5174", "http://localhost:3000" };

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

// Non-blocking background database initialization and seeding
_ = Task.Run(async () =>
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

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
                ");

                // Idempotent QA User Seeding for Development & Cloud
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
                var seedPassword = builder.Configuration["QA_SEED_PASSWORD"] ?? "Password@123";

                var qaAccounts = new (string Username, string Email, string Role)[]
                {
                    ("admin", "admin@fleetflow.io", "ADMIN"),
                    ("manager", "manager@fleetflow.io", "FLEET_MANAGER"),
                    ("worker", "worker@fleetflow.io", "MAINTENANCE_STAFF"),
                    ("customer", "customer@fleetflow.io", "CUSTOMER")
                };

                bool usersAdded = false;
                foreach (var (username, email, roleName) in qaAccounts)
                {
                    var existingUser = await dbContext.Users
                        .Include(u => u.Roles)
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower());

                    if (existingUser == null)
                    {
                        var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name.ToUpper() == roleName.ToUpper());
                        if (role != null)
                        {
                            var isCustomer = roleName == "CUSTOMER";
                            var newUser = new User
                            {
                                Id = Guid.NewGuid(),
                                FullName = isCustomer ? "Customer User" : $"{roleName.Replace("_", " ")} User",
                                Username = username,
                                Email = email,
                                PhoneNumber = isCustomer ? "+1-555-0100" : "+1-555-0199",
                                Address = isCustomer ? "100 FleetFlow Operations Center" : "FleetFlow HQ",
                                DrivingLicenseNumber = isCustomer ? "DL-QA-10001" : "DL-STAFF-001",
                                CreatedAt = DateTime.UtcNow
                            };
                            newUser.PasswordHash = passwordHasher.HashPassword(newUser, seedPassword);
                            newUser.Roles.Add(role);
                            dbContext.Users.Add(newUser);
                            usersAdded = true;
                        }
                    }
                }

                if (usersAdded)
                {
                    await dbContext.SaveChangesAsync();
                }
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

app.UseCors();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", async (IdentityDbContext dbContext) =>
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

    var sslMode = host.Contains("azure.com") ? ";Ssl Mode=Require;Trust Server Certificate=true" : "";
    return $"Host={host};Port={port};Database={dbName};Username={user};Password={password}{sslMode};Timeout=10;Command Timeout=30;";
}
