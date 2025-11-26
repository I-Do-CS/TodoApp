using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TodoApp.API.Entities;

namespace TodoApp.API.Services;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // Prefer storing this as a base64-encoded key in production
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}

public sealed class TokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_settings.Secret))
            throw new InvalidOperationException(
                "JWT secret is not configured. Use a strong base64 secret."
            );

        // Try to treat the Secret as base64. If that fails, fall back to UTF8 bytes but require sufficient length.
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(_settings.Secret);
        }
        catch
        {
            keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
        }

        if (keyBytes.Length < 32) // 256 bits minimum
            throw new InvalidOperationException(
                "JWT secret is too short. Use a 256-bit+ key (base64 preferred)."
            );

        _signingKey = new SymmetricSecurityKey(keyBytes)
        {
            // Optionally set KeyId when you implement rotation: KeyId = "v1"
        };

        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// Create a signed JWT access token (compact) and return the token string.
    /// </summary>
    public string CreateAccessToken(ApplicationUser user, IList<string>? roles = null)
    {
        if (user is null)
            throw new ArgumentNullException(nameof(user));

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Max(1, _settings.AccessTokenExpirationMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials
        );

        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generate a cryptographically secure refresh token (plaintext).
    /// Caller must persist only the hash plus metadata (user, expires, created, revoked).
    /// </summary>
    public string GenerateRefreshTokenPlaintext(int size = 64)
    {
        if (size < 32)
            size = 32;
        var bytes = RandomNumberGenerator.GetBytes(size);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// SHA-256 hash of the refresh token (store this, not the plaintext).
    /// </summary>
    public static string HashToken(string tokenPlaintext)
    {
        if (string.IsNullOrEmpty(tokenPlaintext))
            throw new ArgumentNullException(nameof(tokenPlaintext));
        var bytes = Encoding.UTF8.GetBytes(tokenPlaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
