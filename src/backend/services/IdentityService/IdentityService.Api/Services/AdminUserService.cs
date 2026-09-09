using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;

namespace IdentityService.Api.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "CUSTOMER",
        "FLEET_MANAGER",
        "MAINTENANCE_STAFF",
        "ADMIN"
    };

    public AdminUserService(IdentityDbContext dbContext, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<AdminUserResponse> CreateUserAsync(CreateAdminUserRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required.", nameof(request.Username));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.InitialPassword))
        {
            throw new ArgumentException("Initial password is required.", nameof(request.InitialPassword));
        }

        if (request.InitialPassword.Length < 8)
        {
            throw new ArgumentException("Initial password must be at least 8 characters long.", nameof(request.InitialPassword));
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            throw new ArgumentException("Role is required.", nameof(request.Role));
        }

        var normalizedRole = request.Role.Trim().ToUpper();
        if (!AllowedRoles.Contains(normalizedRole))
        {
            throw new ArgumentException($"Invalid role '{request.Role}'. Allowed roles are: CUSTOMER, FLEET_MANAGER, MAINTENANCE_STAFF, ADMIN.", nameof(request.Role));
        }

        // Profile validation based on role
        string fullName;
        string phoneNumber = string.Empty;
        string address = string.Empty;
        string drivingLicenseNumber = string.Empty;

        if (normalizedRole == "CUSTOMER")
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ArgumentException("Full name is required for customer accounts.", nameof(request.FullName));
            }
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                throw new ArgumentException("Phone number is required for customer accounts.", nameof(request.PhoneNumber));
            }
            if (string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Address is required for customer accounts.", nameof(request.Address));
            }
            if (string.IsNullOrWhiteSpace(request.DrivingLicenseNumber))
            {
                throw new ArgumentException("Driving license number is required for customer accounts.", nameof(request.DrivingLicenseNumber));
            }

            fullName = request.FullName.Trim();
            phoneNumber = request.PhoneNumber.Trim();
            address = request.Address.Trim();
            drivingLicenseNumber = request.DrivingLicenseNumber.Trim();
        }
        else
        {
            // Staff roles: use value if explicitly provided, otherwise safe empty string
            fullName = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName.Trim()
                : string.Empty;

            phoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? request.PhoneNumber.Trim()
                : string.Empty;

            address = !string.IsNullOrWhiteSpace(request.Address)
                ? request.Address.Trim()
                : string.Empty;

            drivingLicenseNumber = !string.IsNullOrWhiteSpace(request.DrivingLicenseNumber)
                ? request.DrivingLicenseNumber.Trim()
                : string.Empty;
        }

        // Uniqueness checks
        var usernameExists = await _dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());
        if (usernameExists)
        {
            throw new DuplicateException("Username is already taken.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower());
        if (emailExists)
        {
            throw new DuplicateException("Email is already registered.");
        }

        // Role retrieval or seeding
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name.ToUpper() == normalizedRole);

        if (role == null)
        {
            role = _dbContext.Roles.Local
                .FirstOrDefault(r => r.Name.Equals(normalizedRole, StringComparison.OrdinalIgnoreCase));

            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = normalizedRole
                };
                _dbContext.Roles.Add(role);
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = phoneNumber,
            Address = address,
            DrivingLicenseNumber = drivingLicenseNumber,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.InitialPassword);
        user.Roles.Add(role);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            DrivingLicenseNumber = user.DrivingLicenseNumber,
            Role = role.Name,
            Roles = new List<string> { role.Name },
            CreatedAt = user.CreatedAt,
            Status = "ACTIVE",
            ProfileImageUrl = user.ProfileImageUrl
        };
    }


    public async Task<AdminUserResponse> UpdateUserAsync(Guid id, UpdateAdminUserRequest request, Guid currentUserId)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            throw new NotFoundException($"User with ID '{id}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required.", nameof(request.Username));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            throw new ArgumentException("Role is required.", nameof(request.Role));
        }

        var normalizedRole = request.Role.Trim().ToUpper();
        if (!AllowedRoles.Contains(normalizedRole))
        {
            throw new ArgumentException($"Invalid role '{request.Role}'. Allowed roles are: CUSTOMER, FLEET_MANAGER, MAINTENANCE_STAFF, ADMIN.", nameof(request.Role));
        }

        // Self / Last Admin Protection
        var isCurrentlyAdmin = user.Roles.Any(r => r.Name.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
        var willBeAdmin = normalizedRole == "ADMIN";

        if (isCurrentlyAdmin && !willBeAdmin)
        {
            if (currentUserId != Guid.Empty && id == currentUserId)
            {
                throw new InvalidOperationException("Administrators cannot demote their own account from the Administrator role.");
            }

            var totalAdminCount = await _dbContext.Users
                .CountAsync(u => u.Roles.Any(r => r.Name.ToUpper() == "ADMIN"));

            if (totalAdminCount <= 1)
            {
                throw new InvalidOperationException("Cannot demote the only remaining Administrator account in the system.");
            }
        }

        // Uniqueness checks excluding current user
        var usernameExists = await _dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower() && u.Id != id);
        if (usernameExists)
        {
            throw new DuplicateException("Username is already taken.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower() && u.Id != id);
        if (emailExists)
        {
            throw new DuplicateException("Email is already registered.");
        }

        // Profile validation based on role
        string fullName;
        string phoneNumber;
        string address;
        string drivingLicenseNumber;

        if (normalizedRole == "CUSTOMER")
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ArgumentException("Full name is required for customer accounts.", nameof(request.FullName));
            }
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                throw new ArgumentException("Phone number is required for customer accounts.", nameof(request.PhoneNumber));
            }
            if (string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Address is required for customer accounts.", nameof(request.Address));
            }
            if (string.IsNullOrWhiteSpace(request.DrivingLicenseNumber))
            {
                throw new ArgumentException("Driving license number is required for customer accounts.", nameof(request.DrivingLicenseNumber));
            }

            fullName = request.FullName.Trim();
            phoneNumber = request.PhoneNumber.Trim();
            address = request.Address.Trim();
            drivingLicenseNumber = request.DrivingLicenseNumber.Trim();
        }
        else
        {
            fullName = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName.Trim()
                : string.Empty;

            phoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? request.PhoneNumber.Trim()
                : string.Empty;

            address = !string.IsNullOrWhiteSpace(request.Address)
                ? request.Address.Trim()
                : string.Empty;

            drivingLicenseNumber = !string.IsNullOrWhiteSpace(request.DrivingLicenseNumber)
                ? request.DrivingLicenseNumber.Trim()
                : string.Empty;
        }

        // Update fields (PasswordHash, CreatedAt, Id remain untouched)
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.FullName = fullName;
        user.PhoneNumber = phoneNumber;
        user.Address = address;
        user.DrivingLicenseNumber = drivingLicenseNumber;

        // Role retrieval or seeding
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name.ToUpper() == normalizedRole);

        if (role == null)
        {
            role = _dbContext.Roles.Local
                .FirstOrDefault(r => r.Name.Equals(normalizedRole, StringComparison.OrdinalIgnoreCase));

            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = normalizedRole
                };
                _dbContext.Roles.Add(role);
            }
        }

        user.Roles.Clear();
        user.Roles.Add(role);

        await _dbContext.SaveChangesAsync();

        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            DrivingLicenseNumber = user.DrivingLicenseNumber,
            Role = role.Name,
            Roles = new List<string> { role.Name },
            CreatedAt = user.CreatedAt,
            Status = "ACTIVE",
            ProfileImageUrl = user.ProfileImageUrl
        };
    }


    public async Task DeleteUserAsync(Guid id, Guid currentUserId)
    {
        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            throw new NotFoundException($"User with ID '{id}' was not found.");
        }

        // Self-deletion protection: Admin cannot delete their own account
        if (currentUserId != Guid.Empty && currentUserId == id)
        {
            throw new InvalidOperationException("An admin cannot delete their own account.");
        }

        // Last-admin protection: Cannot delete the last remaining ADMIN account
        var isCurrentlyAdmin = user.Roles.Any(r => r.Name.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
        if (isCurrentlyAdmin)
        {
            var totalAdminCount = await _dbContext.Users
                .CountAsync(u => u.Roles.Any(r => r.Name.ToUpper() == "ADMIN"));

            if (totalAdminCount <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last remaining ADMIN account.");
            }
        }

        // Remove UserRole mappings and User entity safely
        user.Roles.Clear();
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync()
    {
        var users = await _dbContext.Users
            .Include(u => u.Roles)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return users.Select(u =>
        {
            var primaryRole = u.Roles.FirstOrDefault()?.Name ?? "CUSTOMER";
            return new AdminUserResponse
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Address = u.Address,
                DrivingLicenseNumber = u.DrivingLicenseNumber,
                Role = primaryRole,
                Roles = u.Roles.Select(r => r.Name).ToList(),
                CreatedAt = u.CreatedAt,
                Status = "ACTIVE",
                ProfileImageUrl = u.ProfileImageUrl
            };
        }).ToList();
    }
}

