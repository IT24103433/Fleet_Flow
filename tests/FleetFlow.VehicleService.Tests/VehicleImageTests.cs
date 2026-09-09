using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FleetFlow.VehicleService.Tests;

public class VehicleImageTests : IDisposable
{
    private readonly string _tempUploadDir;

    public VehicleImageTests()
    {
        _tempUploadDir = Path.Combine(Path.GetTempPath(), "fleetflow_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempUploadDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempUploadDir))
            {
                Directory.Delete(_tempUploadDir, true);
            }
        }
        catch
        {
            // ignore
        }
    }

    private (FleetDbContext dbContext, VehicleImageService imageService, Vehicle vehicle) CreateFixture()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new FleetDbContext(options);

        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Sedan",
            Description = "Standard Sedan"
        };
        dbContext.VehicleCategories.Add(category);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA100099",
            LicensePlate = "WP-CAB-9999",
            Make = "Toyota",
            Model = "Camry",
            Year = 2024,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 20000m,
            Transmission = "Automatic",
            FuelType = "Petrol",
            SeatingCapacity = "5",
            HubLocation = "Colombo Central Hub",
            Mileage = 5000,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Vehicles.Add(vehicle);
        dbContext.SaveChanges();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FLEET_UPLOAD_ROOT"]).Returns(_tempUploadDir);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_tempUploadDir);

        var imageService = new VehicleImageService(dbContext, configMock.Object, envMock.Object);

        return (dbContext, imageService, vehicle);
    }

    private static IFormFile CreateMockImageFile(string fileName, string contentType, byte[] headerBytes, int totalLength = 1024)
    {
        var stream = new MemoryStream();
        stream.Write(headerBytes, 0, headerBytes.Length);
        var remaining = totalLength - headerBytes.Length;
        if (remaining > 0)
        {
            stream.Write(new byte[remaining], 0, remaining);
        }
        stream.Position = 0;

        return new FormFile(stream, 0, totalLength, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    [Fact]
    public async Task UploadImageAsync_ValidJpeg_PersistsPortableRelativeUrlAndSavesFile()
    {
        var (dbContext, service, vehicle) = CreateFixture();
        var file = CreateMockImageFile("front_angle.jpg", "image/jpeg", JpegHeader, 2048);

        var response = await service.UploadImageAsync(vehicle.Id, file, "Front 3/4 Angle");

        response.Should().NotBeNull();
        response.VehicleId.Should().Be(vehicle.Id);
        response.OriginalFileName.Should().Be("front_angle.jpg");
        response.ContentType.Should().Be("image/jpeg");
        response.FileSize.Should().Be(2048);
        response.Caption.Should().Be("Front 3/4 Angle");

        // Relative URL must be portable, NOT absolute physical path
        response.RelativeUrl.Should().StartWith("/uploads/vehicles/");
        response.RelativeUrl.Should().NotContain(":");
        response.RelativeUrl.Should().NotContain(_tempUploadDir);

        // Verify entity in DB
        var savedEntity = await dbContext.VehicleImages.FirstOrDefaultAsync(i => i.Id == response.Id);
        savedEntity.Should().NotBeNull();
        savedEntity!.RelativeUrl.Should().Be(response.RelativeUrl);
        savedEntity.FileName.Should().Be(response.FileName);

        // Verify physical file was written to disk at configured FLEET_UPLOAD_ROOT
        var physicalPath = Path.Combine(_tempUploadDir, "vehicles", response.FileName);
        File.Exists(physicalPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadImageAsync_ValidPng_PersistsCorrectExtensionAndRelativeUrl()
    {
        var (dbContext, service, vehicle) = CreateFixture();
        var file = CreateMockImageFile("interior.png", "image/png", PngHeader, 4096);

        var response = await service.UploadImageAsync(vehicle.Id, file, "Cabin Interior");

        response.FileName.Should().EndWith(".png");
        response.RelativeUrl.Should().Be($"/uploads/vehicles/{response.FileName}");
        var physicalPath = Path.Combine(_tempUploadDir, "vehicles", response.FileName);
        File.Exists(physicalPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadImageAsync_Exceeding5Mb_ThrowsArgumentException()
    {
        var (_, service, vehicle) = CreateFixture();
        const int oversized = (5 * 1024 * 1024) + 1;
        var file = CreateMockImageFile("huge.jpg", "image/jpeg", JpegHeader, oversized);

        var act = () => service.UploadImageAsync(vehicle.Id, file, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*5 MB*");
    }

    [Fact]
    public async Task UploadImageAsync_DisallowedMimeType_ThrowsArgumentException()
    {
        var (_, service, vehicle) = CreateFixture();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 dummy"));
        var file = new FormFile(stream, 0, stream.Length, "file", "doc.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var act = () => service.UploadImageAsync(vehicle.Id, file, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported image type*");
    }

    [Fact]
    public async Task UploadImageAsync_CorruptedHeader_ThrowsArgumentException()
    {
        var (_, service, vehicle) = CreateFixture();
        var corruptedHeader = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B };
        var file = CreateMockImageFile("fake.jpg", "image/jpeg", corruptedHeader, 1024);

        var act = () => service.UploadImageAsync(vehicle.Id, file, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*corrupted or not a recognized image*");
    }

    [Fact]
    public async Task UploadImageAsync_NonExistentVehicle_ThrowsNotFoundException()
    {
        var (_, service, _) = CreateFixture();
        var file = CreateMockImageFile("front.jpg", "image/jpeg", JpegHeader, 1024);

        var act = () => service.UploadImageAsync(Guid.NewGuid(), file, null);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetImagesAsync_ReturnsOnlyImagesForRequestedVehicle()
    {
        var (dbContext, service, vehicle1) = CreateFixture();
        var vehicle2 = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "2HGCR2F83HA200088",
            LicensePlate = "WP-CAB-8888",
            Make = "Nissan",
            Model = "Leaf",
            Year = 2023,
            VehicleCategoryId = vehicle1.VehicleCategoryId,
            DailyRate = 15000m,
            Transmission = "Automatic",
            FuelType = "Electric",
            SeatingCapacity = "5",
            HubLocation = "Kandy Hub",
            Mileage = 8000,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Vehicles.Add(vehicle2);
        await dbContext.SaveChangesAsync();

        var file1 = CreateMockImageFile("v1_1.jpg", "image/jpeg", JpegHeader, 1024);
        var file2 = CreateMockImageFile("v1_2.jpg", "image/jpeg", JpegHeader, 1024);
        var file3 = CreateMockImageFile("v2_1.jpg", "image/jpeg", JpegHeader, 1024);

        await service.UploadImageAsync(vehicle1.Id, file1, "Photo 1");
        await service.UploadImageAsync(vehicle1.Id, file2, "Photo 2");
        await service.UploadImageAsync(vehicle2.Id, file3, "V2 Photo");

        var v1Images = await service.GetImagesAsync(vehicle1.Id);
        var v2Images = await service.GetImagesAsync(vehicle2.Id);

        v1Images.Should().HaveCount(2);
        v2Images.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteImageAsync_RemovesEntityAndPhysicalFile()
    {
        var (dbContext, service, vehicle) = CreateFixture();
        var file = CreateMockImageFile("todelete.jpg", "image/jpeg", JpegHeader, 1024);

        var uploaded = await service.UploadImageAsync(vehicle.Id, file, "Temporary");
        var physicalPath = Path.Combine(_tempUploadDir, "vehicles", uploaded.FileName);
        File.Exists(physicalPath).Should().BeTrue();

        await service.DeleteImageAsync(vehicle.Id, uploaded.Id);

        // DB entity must be gone
        var existsInDb = await dbContext.VehicleImages.AnyAsync(i => i.Id == uploaded.Id);
        existsInDb.Should().BeFalse();

        // Physical file must be removed
        File.Exists(physicalPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteImageAsync_NonExistentImage_ThrowsNotFoundException()
    {
        var (_, service, vehicle) = CreateFixture();

        var act = () => service.DeleteImageAsync(vehicle.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAllImagesForVehicleAsync_RemovesAllAssociatedDbRecordsAndPhysicalFiles()
    {
        var (dbContext, service, vehicle) = CreateFixture();
        var file1 = CreateMockImageFile("img1.jpg", "image/jpeg", JpegHeader, 1024);
        var file2 = CreateMockImageFile("img2.png", "image/png", PngHeader, 2048);

        var uploaded1 = await service.UploadImageAsync(vehicle.Id, file1, "Photo 1");
        var uploaded2 = await service.UploadImageAsync(vehicle.Id, file2, "Photo 2");

        var path1 = Path.Combine(_tempUploadDir, "vehicles", uploaded1.FileName);
        var path2 = Path.Combine(_tempUploadDir, "vehicles", uploaded2.FileName);

        File.Exists(path1).Should().BeTrue();
        File.Exists(path2).Should().BeTrue();

        await service.DeleteAllImagesForVehicleAsync(vehicle.Id);

        // All database records for vehicle must be deleted
        var remainingInDb = await dbContext.VehicleImages.Where(i => i.VehicleId == vehicle.Id).ToListAsync();
        remainingInDb.Should().BeEmpty();

        // All physical files must be removed
        File.Exists(path1).Should().BeFalse();
        File.Exists(path2).Should().BeFalse();
    }

    [Fact]
    public void VehicleImagesController_AuthorizationDecorators_AreConfiguredProperly()
    {
        var controllerType = typeof(VehicleImagesController);

        var uploadMethod = controllerType.GetMethod(nameof(VehicleImagesController.UploadImage));
        uploadMethod.Should().NotBeNull();
        var uploadAuth = uploadMethod!.GetCustomAttribute<AuthorizeAttribute>();
        uploadAuth.Should().NotBeNull();
        uploadAuth!.Roles.Should().Be("ADMIN,FLEET_MANAGER");

        var deleteMethod = controllerType.GetMethod(nameof(VehicleImagesController.DeleteImage));
        deleteMethod.Should().NotBeNull();
        var deleteAuth = deleteMethod!.GetCustomAttribute<AuthorizeAttribute>();
        deleteAuth.Should().NotBeNull();
        deleteAuth!.Roles.Should().Be("ADMIN,FLEET_MANAGER");

        var getMethod = controllerType.GetMethod(nameof(VehicleImagesController.GetImages));
        getMethod.Should().NotBeNull();
        var allowAnonymous = getMethod!.GetCustomAttribute<AllowAnonymousAttribute>();
        allowAnonymous.Should().NotBeNull();
    }
}
