using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FleetFlow.IdentityService.Tests;

public class ProfilePictureTests : IDisposable
{
    private readonly string _tempUploadDir;

    public ProfilePictureTests()
    {
        _tempUploadDir = Path.Combine(Path.GetTempPath(), "fleetflow_identity_tests_" + Guid.NewGuid().ToString("N"));
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

    private (IdentityDbContext dbContext, ProfileImageService service, User user) CreateFixture()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new IdentityDbContext(options);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "johndoe",
            Email = "john@example.com",
            PasswordHash = "dummyhash",
            FullName = "John Doe",
            PhoneNumber = "0771234567",
            Address = "123 Galle Road, Colombo",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["IDENTITY_UPLOAD_ROOT"]).Returns(_tempUploadDir);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_tempUploadDir);

        var service = new ProfileImageService(dbContext, configMock.Object, envMock.Object);

        return (dbContext, service, user);
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
    public async Task UploadProfilePictureAsync_ValidJpeg_PersistsPortableRelativeUrlAndPhysicalFile()
    {
        var (dbContext, service, user) = CreateFixture();
        var file = CreateMockImageFile("avatar.jpg", "image/jpeg", JpegHeader, 2048);

        var relativeUrl = await service.UploadProfilePictureAsync(user.Id, file);

        relativeUrl.Should().StartWith("/uploads/profiles/");
        relativeUrl.Should().NotContain(":");
        relativeUrl.Should().NotContain(_tempUploadDir);

        // Verify DB update
        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.ProfileImageUrl.Should().Be(relativeUrl);

        // Verify physical file was created in configured directory
        var fileName = Path.GetFileName(relativeUrl);
        var physicalPath = Path.Combine(_tempUploadDir, "profiles", fileName);
        File.Exists(physicalPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadProfilePictureAsync_ReplacesOldPhoto_DeletesPreviousPhysicalFile()
    {
        var (dbContext, service, user) = CreateFixture();
        var file1 = CreateMockImageFile("first.jpg", "image/jpeg", JpegHeader, 1024);
        var file2 = CreateMockImageFile("second.png", "image/png", PngHeader, 2048);

        var url1 = await service.UploadProfilePictureAsync(user.Id, file1);
        var path1 = Path.Combine(_tempUploadDir, "profiles", Path.GetFileName(url1));
        File.Exists(path1).Should().BeTrue();

        // Upload second photo
        var url2 = await service.UploadProfilePictureAsync(user.Id, file2);
        var path2 = Path.Combine(_tempUploadDir, "profiles", Path.GetFileName(url2));

        // First file should be deleted, second file must exist
        File.Exists(path1).Should().BeFalse();
        File.Exists(path2).Should().BeTrue();

        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        updatedUser!.ProfileImageUrl.Should().Be(url2);
    }

    [Fact]
    public async Task UploadProfilePictureAsync_OversizedFile_ThrowsArgumentException()
    {
        var (_, service, user) = CreateFixture();
        const int oversized = (5 * 1024 * 1024) + 1;
        var file = CreateMockImageFile("large.jpg", "image/jpeg", JpegHeader, oversized);

        var act = () => service.UploadProfilePictureAsync(user.Id, file);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*5 MB*");
    }

    [Fact]
    public async Task UploadProfilePictureAsync_DisallowedMime_ThrowsArgumentException()
    {
        var (_, service, user) = CreateFixture();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("not an image"));
        var file = new FormFile(stream, 0, stream.Length, "file", "text.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var act = () => service.UploadProfilePictureAsync(user.Id, file);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported image type*");
    }

    [Fact]
    public async Task DeleteProfilePictureAsync_RemovesPhysicalFileAndClearsDbField()
    {
        var (dbContext, service, user) = CreateFixture();
        var file = CreateMockImageFile("to_delete.jpg", "image/jpeg", JpegHeader, 1024);

        var url = await service.UploadProfilePictureAsync(user.Id, file);
        var physicalPath = Path.Combine(_tempUploadDir, "profiles", Path.GetFileName(url));
        File.Exists(physicalPath).Should().BeTrue();

        await service.DeleteProfilePictureAsync(user.Id);

        // DB field cleared
        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        updatedUser!.ProfileImageUrl.Should().BeNull();

        // Physical file removed
        File.Exists(physicalPath).Should().BeFalse();
    }

    [Fact]
    public void AuthController_ProfilePictureEndpoints_RequireAuthorization()
    {
        var controllerType = typeof(AuthController);

        var uploadMethod = controllerType.GetMethod(nameof(AuthController.UploadProfilePicture));
        uploadMethod.Should().NotBeNull();
        uploadMethod!.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();

        var getMethod = controllerType.GetMethod(nameof(AuthController.GetProfilePicture));
        getMethod.Should().NotBeNull();
        getMethod!.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();

        var deleteMethod = controllerType.GetMethod(nameof(AuthController.DeleteProfilePicture));
        deleteMethod.Should().NotBeNull();
        deleteMethod!.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }
}
