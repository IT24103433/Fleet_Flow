using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;

namespace IdentityService.Api.Services;

public class RegistrationService : IRegistrationService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;

    public RegistrationService(IdentityDbContext dbContext, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
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
            Username = request.Username,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.Roles.Add(role);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };
    }
}
