using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Messaging;
using IdentityService.Api.Messaging.Events;

namespace IdentityService.Api.Services;

public class RegistrationService : IRegistrationService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IKafkaProducerService? _kafkaProducer;
    private readonly KafkaSettings _kafkaSettings;

    public RegistrationService(
        IdentityDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IKafkaProducerService? kafkaProducer = null,
        IOptions<KafkaSettings>? kafkaSettings = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _kafkaProducer = kafkaProducer;
        _kafkaSettings = kafkaSettings?.Value ?? new KafkaSettings();
    }

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name is required.", nameof(request.FullName));
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new ArgumentException("Phone number is required.", nameof(request.PhoneNumber));
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new ArgumentException("Address is required.", nameof(request.Address));
        }

        if (string.IsNullOrWhiteSpace(request.DrivingLicenseNumber))
        {
            throw new ArgumentException("Driving license number is required.", nameof(request.DrivingLicenseNumber));
        }

        var usernameExists = await _dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());
        if (usernameExists)
        {
            throw new DuplicateException("Username is already taken.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());
        if (emailExists)
        {
            throw new DuplicateException("Email is already registered.");
        }

        var customerRoleName = "CUSTOMER";
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name.ToUpper() == customerRoleName.ToUpper());

        if (role == null)
        {
            role = _dbContext.Roles.Local
                .FirstOrDefault(r => r.Name.Equals(customerRoleName, StringComparison.OrdinalIgnoreCase));

            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = customerRoleName
                };
                _dbContext.Roles.Add(role);
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Address = request.Address.Trim(),
            DrivingLicenseNumber = request.DrivingLicenseNumber.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.Roles.Add(role);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        if (_kafkaProducer != null)
        {
            var createdEvent = new UserCreatedEvent
            {
                UserId = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                Role = role.Name,
                Roles = new List<string> { role.Name },
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
            _ = _kafkaProducer.PublishAsync(_kafkaSettings.UserEventsTopic, user.Id.ToString(), createdEvent);
        }

        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            DrivingLicenseNumber = user.DrivingLicenseNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt
        };
    }
}
