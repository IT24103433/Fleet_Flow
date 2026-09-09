using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;

namespace IdentityService.Api.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthenticationService(
        IdentityDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var identifier = request.UsernameOrEmail?.Trim();
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidCredentialsException();
        }

        // Find user by username or email case-insensitively
        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == identifier.ToLower() 
                                   || u.Email.ToLower() == identifier.ToLower());

        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        // Verify password
        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        // Enforce login channel restrictions
        var channel = request.LoginChannel?.Trim().ToLowerInvariant() ?? "customer";
        var userRoleNames = user.Roles.Select(r => r.Name.ToUpperInvariant()).ToList();
        var staffRoles = new HashSet<string> { "FLEET_MANAGER", "MAINTENANCE_STAFF", "ADMIN" };

        if (channel == "customer")
        {
            // Only CUSTOMER role is allowed through the customer channel
            if (!userRoleNames.Contains("CUSTOMER") || userRoleNames.Any(r => staffRoles.Contains(r)))
            {
                throw new PortalAccessDeniedException(
                    "Staff and administrator accounts must use the Staff Portal to sign in.");
            }
        }
        else if (channel == "staff")
        {
            // Only staff roles are allowed through the staff channel
            if (!userRoleNames.Any(r => staffRoles.Contains(r)))
            {
                throw new PortalAccessDeniedException(
                    "Customer accounts do not have access to the Staff Portal. Please use the Customer Portal to sign in.");
            }
        }
        else
        {
            throw new PortalAccessDeniedException($"Invalid login channel '{request.LoginChannel}'. Allowed channels are 'customer' or 'staff'.");
        }

        // Retrieve JWT settings
        var jwtSection = _configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var keyStr = jwtSection["Key"];
        var expiryInMinutesStr = jwtSection["ExpiryInMinutes"];

        if (string.IsNullOrEmpty(keyStr) || Encoding.UTF8.GetByteCount(keyStr) < 32)
        {
            throw new InvalidOperationException("JWT Signing Key must be configured and must be at least 32 bytes (256 bits) long.");
        }

        if (!double.TryParse(expiryInMinutesStr, out var expiryInMinutes))
        {
            expiryInMinutes = 60; // Default to 60 minutes if not specified/invalid
        }

        var expiration = DateTime.UtcNow.AddMinutes(expiryInMinutes);

        // Build Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add Role Claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse
        {
            Token = tokenString,
            Expiration = expiration,
            User = new UserResponse
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
            }
        };
    }

}
