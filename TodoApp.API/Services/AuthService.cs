using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoApp.API.Database;
using TodoApp.API.DTOs.ApplicationUser;
using TodoApp.API.Entities;
using TodoApp.API.Shared;

namespace TodoApp.API.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<ApplicationRole> roleManager,
    TokenService tokenService,
    ApplicationDbContext dbContext,
    IOptions<JwtSettings> jwtOptions,
    ILogger<AuthService> logger
)
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    // Result model for auth operations
    public sealed record AuthenticationResult(
        bool Succeeded,
        string? AccessToken = null,
        string? RefreshToken = null,
        DateTime? AccessTokenExpiresUtc = null,
        DateTime? RefreshTokenExpiresUtc = null,
        IReadOnlyCollection<string>? Roles = null,
        IEnumerable<string>? Errors = null
    );

    // DTO used for login
    public sealed record LoginRequest(string Email, string Password, string? IpAddress = null);

    // DTO used for refresh requests
    public sealed record RefreshRequest(string RefreshToken, string? IpAddress = null);

    // Register a new user and optionally add the default "User" role
    public async Task<IdentityResult> RegisterAsync(
        ApplicationUserRegisterDto registerDto,
        CancellationToken cancellationToken = default
    )
    {
        var user = new ApplicationUser
        {
            UserName = registerDto.Email,
            Email = registerDto.Email,
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName ?? string.Empty,
        };

        var result = await userManager.CreateAsync(user, registerDto.Password);
        if (result.Succeeded)
        {
            // Add default role - change as appropriate for your app
            if (await RoleExistsAsync("User", cancellationToken))
            {
                await userManager.AddToRoleAsync(user, "User");
            }
        }

        return result;

        async Task<bool> RoleExistsAsync(string roleName, CancellationToken ct)
        {
            try
            {
                return await roleManager.RoleExistsAsync(roleName);
            }
            catch
            {
                // If RoleManager is not registered or fails, return false and skip adding the role.
                logger.LogWarning("RoleManager unavailable while assigning default role.");
                return false;
            }
        }
    }

    // Promote a user to Admin (idempotent). Requires RoleManager to ensure role exists.
    public async Task<IdentityResult> PromoteToAdminAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return IdentityResult.Failed(
                new IdentityError[]
                {
                    new()
                    {
                        Code = "NotFound",
                        Description = $"User with id '{userId}' not found.",
                    },
                }
            );
        }

        // Ensure the Admin role exists
        if (!await roleManager.RoleExistsAsync(ApplicationUserRoles.Admin))
        {
            var createRoleResult = await roleManager.CreateAsync(
                new ApplicationRole { Name = ApplicationUserRoles.Admin }
            );
            if (!createRoleResult.Succeeded)
                return createRoleResult;
        }

        if (await userManager.IsInRoleAsync(user, ApplicationUserRoles.Admin))
            return IdentityResult.Success;

        return await userManager.AddToRoleAsync(user, ApplicationUserRoles.Admin);
    }

    // Optional: demote an admin back to regular user (idempotent)
    public async Task<IdentityResult> DemoteFromAdminAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return IdentityResult.Failed(
                new IdentityError[]
                {
                    new()
                    {
                        Code = "NotFound",
                        Description = $"User with id '{userId}' not found.",
                    },
                }
            );
        }

        if (!await userManager.IsInRoleAsync(user, ApplicationUserRoles.Admin))
            return IdentityResult.Success;

        return await userManager.RemoveFromRoleAsync(user, ApplicationUserRoles.Admin);
    }

    // Authenticate user and issue access + refresh tokens (persist refresh token hashed)
    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return new AuthenticationResult(false, Errors: new[] { "Invalid credentials." });

        var check = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: false
        );
        if (!check.Succeeded)
            return new AuthenticationResult(false, Errors: new[] { "Invalid credentials." });

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles);
        var accessExpires = DateTime.UtcNow.AddMinutes(
            Math.Max(1, _jwtSettings.AccessTokenExpirationMinutes)
        );

        // Create refresh token (plaintext to return to client), store only the hash
        var refreshPlain = tokenService.GenerateRefreshTokenPlaintext();
        var refreshHash = TokenService.HashToken(refreshPlain);
        var refreshExpires = DateTime.UtcNow.AddDays(
            Math.Max(1, _jwtSettings.RefreshTokenExpirationDays)
        );

        var refreshEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = refreshHash,
            UserId = user.Id!,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = request.IpAddress ?? "unknown",
        };

        dbContext.RefreshTokens.Add(refreshEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticationResult(
            true,
            AccessToken: accessToken,
            RefreshToken: refreshPlain,
            AccessTokenExpiresUtc: accessExpires,
            RefreshTokenExpiresUtc: refreshExpires,
            Roles: roles.ToList().AsReadOnly()
        );
    }

    // Rotate refresh token: validate provided plaintext refresh token, revoke it, issue new pair
    public async Task<AuthenticationResult> RefreshTokenAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var incomingHash = TokenService.HashToken(request.RefreshToken);
        var tokenEntity = await dbContext
            .RefreshTokens.AsQueryable()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash, cancellationToken);

        if (tokenEntity is null || !tokenEntity.IsActive)
            return new AuthenticationResult(
                false,
                Errors: new[] { "Invalid or expired refresh token." }
            );

        // load user
        var user = tokenEntity.User ?? await userManager.FindByIdAsync(tokenEntity.UserId);
        if (user is null)
            return new AuthenticationResult(
                false,
                Errors: new[] { "User not found for refresh token." }
            );

        // Revoke the used refresh token and mark replacement
        tokenEntity.RevokedAtUtc = DateTime.UtcNow;
        tokenEntity.RevokedByIp = request.IpAddress ?? "unknown";

        // Create new refresh token (rotation)
        var newPlain = tokenService.GenerateRefreshTokenPlaintext();
        var newHash = TokenService.HashToken(newPlain);
        var newExpires = DateTime.UtcNow.AddDays(
            Math.Max(1, _jwtSettings.RefreshTokenExpirationDays)
        );

        var newRt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = newHash,
            UserId = tokenEntity.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = newExpires,
            CreatedByIp = request.IpAddress ?? "unknown",
            ReplacedByTokenHash = null,
        };

        tokenEntity.ReplacedByTokenHash = newHash;

        dbContext.RefreshTokens.Update(tokenEntity);
        dbContext.RefreshTokens.Add(newRt);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var newAccess = tokenService.CreateAccessToken(user, roles);
        var accessExpires = DateTime.UtcNow.AddMinutes(
            Math.Max(1, _jwtSettings.AccessTokenExpirationMinutes)
        );

        return new AuthenticationResult(
            true,
            AccessToken: newAccess,
            RefreshToken: newPlain,
            AccessTokenExpiresUtc: accessExpires,
            RefreshTokenExpiresUtc: newExpires,
            Roles: roles.ToList().AsReadOnly()
        );
    }

    // Revoke a refresh token (by plaintext). Useful for logout.
    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshTokenPlain,
        string? ipAddress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(refreshTokenPlain))
            return false;

        var hash = TokenService.HashToken(refreshTokenPlain);
        var tokenEntity = await dbContext.RefreshTokens.FirstOrDefaultAsync(
            rt => rt.TokenHash == hash,
            cancellationToken
        );
        if (tokenEntity is null || !tokenEntity.IsActive)
            return false;

        tokenEntity.RevokedAtUtc = DateTime.UtcNow;
        tokenEntity.RevokedByIp = ipAddress ?? "unknown";

        dbContext.RefreshTokens.Update(tokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
