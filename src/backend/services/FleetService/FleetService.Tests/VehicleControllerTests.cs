using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Xunit;

namespace FleetService.Tests;

public class VehicleControllerTests : IClassFixture<CustomWebApplicationFactory<VehiclesController>>
{
    private readonly CustomWebApplicationFactory<VehiclesController> _factory;
    private const string JwtKey = "TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError";
    private const string Issuer = "FleetFlow.IdentityService";
    private const string Audience = "FleetFlow.Client";

    public VehicleControllerTests(CustomWebApplicationFactory<VehiclesController> factory)
    {
        _factory = factory;
    }

    private static string GenerateToken(string username, string email, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetCategories_ReturnsSeededCategories_Status200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/vehicle-categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<VehicleCategoryResponse>>();
        Assert.NotNull(categories);
        Assert.True(categories.Count >= 5);
        Assert.Contains(categories, c => c.Name == "Executive Sedan");
        Assert.Contains(categories, c => c.Name == "Full-Size SUV");
        Assert.Contains(categories, c => c.Name == "Commercial Cargo");
        Assert.Contains(categories, c => c.Name == "Compact EV");
        Assert.Contains(categories, c => c.Name == "Premium Coupe");
    }

    [Fact]
    public async Task CategorySeeding_IsIdempotent_DoesNotCreateDuplicates()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var defaultCategories = new (string Name, string Description)[]
        {
            ("Executive Sedan", "Luxury and executive passenger sedans."),
            ("Full-Size SUV", "Spacious premium SUVs."),
            ("Commercial Cargo", "Cargo vans."),
            ("Compact EV", "Electric compacts."),
            ("Premium Coupe", "Coupes.")
        };

        // Act - Re-run seeding logic
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
            }
        }
        await dbContext.SaveChangesAsync();

        // Assert
        var executiveCategories = await dbContext.VehicleCategories
            .Where(c => c.Name == "Executive Sedan")
            .ToListAsync();
        Assert.Single(executiveCategories);
    }

    [Fact]
    public async Task PostVehicle_WithFleetManagerJwt_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("managerUser", "manager@fleetflow.io", "FLEET_MANAGER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029101",
            LicensePlate = "FL-101-AA",
            Make = "Aero",
            Model = "Apex Executive",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Plug-in Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 5000
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(created);
        Assert.Equal("1HGCR2F83HA029101", created.Vin);
        Assert.Equal("FL-101-AA", created.LicensePlate);
    }

    [Fact]
    public async Task PostVehicle_WithAdminJwt_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029102",
            LicensePlate = "FL-102-BB",
            Make = "Summit",
            Model = "Horizon Pro",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 130.00m,
            Transmission = "Intelligent AWD",
            FuelType = "Dual-Motor Electric",
            SeatingCapacity = "7 Passengers",
            HubLocation = "Uptown Station",
            Mileage = 8200
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(created);
        Assert.Equal("1HGCR2F83HA029102", created.Vin);
    }

    [Fact]
    public async Task PostVehicle_WithCustomerJwt_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("customerUser", "customer@fleetflow.io", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029103",
            LicensePlate = "FL-103-CC",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_WithMaintenanceStaffJwt_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("workerUser", "worker@fleetflow.io", "MAINTENANCE_STAFF");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029104",
            LicensePlate = "FL-104-DD",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029105",
            LicensePlate = "FL-105-EE",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_DuplicateVin_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        var request1 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029106",
            LicensePlate = "FL-106-FF",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        var request2 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029106", // duplicate VIN
            LicensePlate = "FL-107-GG",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        await client.PostAsJsonAsync("/api/vehicles", request1);
        var response = await client.PostAsJsonAsync("/api/vehicles", request2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_DuplicateLicensePlate_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        var request1 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029108",
            LicensePlate = "FL-108-HH",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        var request2 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029109",
            LicensePlate = "FL-108-HH", // duplicate license plate
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        await client.PostAsJsonAsync("/api/vehicles", request1);
        var response = await client.PostAsJsonAsync("/api/vehicles", request2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_MissingVehicleCategory_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029110",
            LicensePlate = "FL-110-JJ",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = Guid.NewGuid(), // non-existent
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVehicle_InvalidVehicleData_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateVehicleRequest
        {
            Vin = "SHORT_VIN", // invalid length
            LicensePlate = "", // required
            Make = "",
            Model = "",
            Year = 1800, // out of range
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = -5m, // invalid negative rate
            Transmission = "",
            FuelType = "",
            SeatingCapacity = "",
            HubLocation = "",
            Mileage = -10
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/vehicles");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GetVehicleById_ExistingId_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        var postResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029111",
            LicensePlate = "FL-111-KK",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        });

        var created = await postResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        // Act (anonymous read)
        var unauthenticatedClient = _factory.CreateClient();
        var response = await unauthenticatedClient.GetAsync($"/api/vehicles/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task GetVehicleById_MissingId_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/vehicles/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_SearchFilterPagination_Returns200OkWithFilteredData()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoriesResponse = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categoriesResponse!.First().Id;

        await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029112",
            LicensePlate = "FL-112-LL",
            Make = "SearchMakeUnique",
            Model = "SearchModelUnique",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Diesel",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        });

        // Act
        var unauthenticatedClient = _factory.CreateClient();
        var response = await unauthenticatedClient.GetAsync("/api/vehicles?searchTerm=SearchMakeUnique&fuel=Diesel&page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        Assert.NotNull(results);
        Assert.Contains(results, v => v.Make == "SearchMakeUnique");
    }

    [Fact]
    public async Task UpdateVehicle_WithFleetManagerJwt_Returns200OkAndPersists()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029120",
            LicensePlate = "FL-120-AA",
            Make = "Toyota",
            Model = "Camry",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 95.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1200
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        // Act - Update as FLEET_MANAGER
        var managerToken = GenerateToken("managerUser", "manager@fleetflow.io", "FLEET_MANAGER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029120",
            LicensePlate = "FL-120-ZZ",
            Make = "Toyota",
            Model = "Camry XSE",
            Year = 2025,
            VehicleCategoryId = categoryId,
            DailyRate = 110.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "North Station",
            Mileage = 2500
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/vehicles/{created!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(updated);
        Assert.Equal("FL-120-ZZ", updated.LicensePlate);
        Assert.Equal("Camry XSE", updated.Model);
        Assert.Equal(2025, updated.Year);
        Assert.Equal(110.00m, updated.DailyRate);
        Assert.Equal("North Station", updated.HubLocation);
        Assert.Equal(2500, updated.Mileage);
        Assert.NotNull(updated.UpdatedAt);

        // Verify persistence with unauthenticated GET
        var unauthenticatedClient = _factory.CreateClient();
        var fetchResponse = await unauthenticatedClient.GetAsync($"/api/vehicles/{created.Id}");
        var fetched = await fetchResponse.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.Equal("FL-120-ZZ", fetched!.LicensePlate);
        Assert.Equal("Camry XSE", fetched.Model);
    }

    [Fact]
    public async Task UpdateVehicle_WithAdminJwt_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029121",
            LicensePlate = "FL-121-BB",
            Make = "BMW",
            Model = "330i",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 150.00m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Downtown Hub",
            Mileage = 5000
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029121",
            LicensePlate = "FL-121-BB",
            Make = "BMW",
            Model = "330e Hybrid",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 165.00m,
            Transmission = "Automatic",
            FuelType = "Plug-in Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Downtown Hub",
            Mileage = 5200
        };

        // Act
        var updateResponse = await client.PutAsJsonAsync($"/api/vehicles/{created!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.NotNull(updated);
        Assert.Equal("330e Hybrid", updated.Model);
        Assert.Equal(165.00m, updated.DailyRate);
    }

    [Fact]
    public async Task UpdateVehicle_WithCustomerJwt_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029122",
            LicensePlate = "FL-122-CC",
            Make = "Nissan",
            Model = "Leaf",
            Year = 2023,
            VehicleCategoryId = categoryId,
            DailyRate = 70.00m,
            Transmission = "Single-Speed",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Eco Hub",
            Mileage = 8000
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        // Act with Customer token
        var customerToken = GenerateToken("customerUser", "customer@fleetflow.io", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029122",
            LicensePlate = "FL-122-CC",
            Make = "Nissan",
            Model = "Leaf Plus",
            Year = 2023,
            VehicleCategoryId = categoryId,
            DailyRate = 75.00m,
            Transmission = "Single-Speed",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Eco Hub",
            Mileage = 8000
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{created!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_WithMaintenanceStaffJwt_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var workerToken = GenerateToken("workerUser", "worker@fleetflow.io", "MAINTENANCE_STAFF");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", workerToken);

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029123",
            LicensePlate = "FL-123-DD",
            Make = "Ford",
            Model = "Transit",
            Year = 2023,
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = 110.00m,
            Transmission = "Automatic",
            FuelType = "Diesel",
            SeatingCapacity = "2 Passengers",
            HubLocation = "Logistics Terminal",
            Mileage = 20000
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{Guid.NewGuid()}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029124",
            LicensePlate = "FL-124-EE",
            Make = "Ford",
            Model = "Transit",
            Year = 2023,
            VehicleCategoryId = Guid.NewGuid(),
            DailyRate = 110.00m,
            Transmission = "Automatic",
            FuelType = "Diesel",
            SeatingCapacity = "2 Passengers",
            HubLocation = "Logistics Terminal",
            Mileage = 20000
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{Guid.NewGuid()}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_NonExistentId_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029125",
            LicensePlate = "FL-125-FF",
            Make = "Audi",
            Model = "A4",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 130.00m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 3000
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{Guid.NewGuid()}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_DuplicateVinOnAnotherVehicle_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        // Vehicle 1
        var createResponse1 = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029126",
            LicensePlate = "FL-126-GG",
            Make = "Hyundai",
            Model = "Ioniq 5",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 100.00m,
            Transmission = "Single-Speed",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1000
        });
        var vehicle1 = await createResponse1.Content.ReadFromJsonAsync<VehicleResponse>();

        // Vehicle 2
        await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029127",
            LicensePlate = "FL-127-HH",
            Make = "Kia",
            Model = "EV6",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 105.00m,
            Transmission = "Single-Speed",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1000
        });

        // Act - Try updating Vehicle 1 with Vehicle 2's VIN
        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA029127", // duplicate VIN
            LicensePlate = vehicle1!.LicensePlate,
            Make = vehicle1.Make,
            Model = vehicle1.Model,
            Year = vehicle1.Year,
            VehicleCategoryId = categoryId,
            DailyRate = vehicle1.DailyRate,
            Transmission = vehicle1.Transmission,
            FuelType = vehicle1.FuelType,
            SeatingCapacity = vehicle1.SeatingCapacity,
            HubLocation = vehicle1.HubLocation,
            Mileage = vehicle1.Mileage
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle1.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_DuplicateLicensePlateOnAnotherVehicle_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        // Vehicle 1
        var createResponse1 = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029128",
            LicensePlate = "FL-128-II",
            Make = "Volvo",
            Model = "XC60",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 140.00m,
            Transmission = "Automatic",
            FuelType = "Plug-in Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1000
        });
        var vehicle1 = await createResponse1.Content.ReadFromJsonAsync<VehicleResponse>();

        // Vehicle 2
        await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029129",
            LicensePlate = "FL-129-JJ",
            Make = "Volvo",
            Model = "XC90",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 180.00m,
            Transmission = "Automatic",
            FuelType = "Plug-in Hybrid",
            SeatingCapacity = "7 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1000
        });

        // Act - Try updating Vehicle 1 with Vehicle 2's Plate
        var updateRequest = new UpdateVehicleRequest
        {
            Vin = vehicle1!.Vin,
            LicensePlate = "FL-129-JJ", // duplicate plate
            Make = vehicle1.Make,
            Model = vehicle1.Model,
            Year = vehicle1.Year,
            VehicleCategoryId = categoryId,
            DailyRate = vehicle1.DailyRate,
            Transmission = vehicle1.Transmission,
            FuelType = vehicle1.FuelType,
            SeatingCapacity = vehicle1.SeatingCapacity,
            HubLocation = vehicle1.HubLocation,
            Mileage = vehicle1.Mileage
        };

        var response = await client.PutAsJsonAsync($"/api/vehicles/{vehicle1.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_RetainingOwnVinAndPlate_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029130",
            LicensePlate = "FL-130-KK",
            Make = "Subaru",
            Model = "Outback",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 95.00m,
            Transmission = "CVT",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Mountain Hub",
            Mileage = 4000
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = created!.Vin,
            LicensePlate = created.LicensePlate,
            Make = "Subaru",
            Model = "Outback Wilderness",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 105.00m,
            Transmission = "CVT",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Mountain Hub",
            Mileage = 4500
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.Equal("Outback Wilderness", updated!.Model);
        Assert.Equal(105.00m, updated.DailyRate);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidCategory_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029131",
            LicensePlate = "FL-131-LL",
            Make = "Mazda",
            Model = "CX-5",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 90.00m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 2000
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = created!.Vin,
            LicensePlate = created.LicensePlate,
            Make = created.Make,
            Model = created.Model,
            Year = created.Year,
            VehicleCategoryId = Guid.NewGuid(), // Non-existent category
            DailyRate = created.DailyRate,
            Transmission = created.Transmission,
            FuelType = created.FuelType,
            SeatingCapacity = created.SeatingCapacity,
            HubLocation = created.HubLocation,
            Mileage = created.Mileage
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidFields_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminToken = GenerateToken("adminUser", "admin@fleetflow.io", "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var categories = await client.GetFromJsonAsync<List<VehicleCategoryResponse>>("/api/vehicle-categories");
        var categoryId = categories!.First().Id;

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029132",
            LicensePlate = "FL-132-MM",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 120.00m,
            Transmission = "Single-Speed",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Hub",
            Mileage = 1000
        });
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = "SHORT", // invalid VIN length
            LicensePlate = "", // required
            Make = "",
            Model = "",
            Year = 1800, // out of range
            VehicleCategoryId = categoryId,
            DailyRate = -50m, // invalid negative rate
            Transmission = "",
            FuelType = "",
            SeatingCapacity = "",
            HubLocation = "",
            Mileage = -5 // negative mileage
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{created!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

