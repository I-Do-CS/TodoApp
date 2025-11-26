using System.Runtime;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TodoApp.API.Database;
using TodoApp.API.Entities;
using TodoApp.API.Services;

namespace TodoApp.API.Extensions;

public static class ServiceExtensions
{
    public static void AddJwtAuthAndPolicies(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Bind JwtSettings and make available via IOptions<JwtSettings>
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        var jwtSettings =
            configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings not configured");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = true;

                // Try to treat the Secret as base64. If that fails, fall back to UTF8 bytes but require sufficient length.
                byte[] key;
                try
                {
                    key = Convert.FromBase64String(jwtSettings.Secret);
                }
                catch
                {
                    key = Encoding.UTF8.GetBytes(jwtSettings.Secret);
                }

                if (key.Length < 32) // 256 bits minimum
                    throw new InvalidOperationException(
                        "JWT secret is too short. Use a 256-bit+ key (base64 preferred)."
                    );

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    ValidateLifetime = true,
                    // Keep clock skew small (allow for minor clock drift). Consider TimeSpan.Zero if strict.
                    ClockSkew = TimeSpan.FromMinutes(1),

                    // Map incoming claim types to framework identity:
                    // - TokenService emits "sub" (JwtRegisteredClaimNames.Sub) for user id
                    // - TokenService uses ClaimTypes.Role for roles
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        // Only role-based policies using AddAuthorizationBuilder
        var authorizationBuilder = services.AddAuthorizationBuilder();

        // Simple role policy: only Admins
        authorizationBuilder.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));

        // Example composite role policy: Admin or Manager
        //authorizationBuilder.AddPolicy(
        //    "RequireAdminOrManagerRole",
        //    policy => policy.RequireRole("Admin", "Manager")
        //);

        // Example policy for general authenticated users with the "User" role
        //authorizationBuilder.AddPolicy("RequireUserRole", policy => policy.RequireRole("User"));
    }

    public static void AddRuntimeServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<TokenService>();
        services.AddScoped<UserService>();
    }

    public static void AddConfiguredDbContext(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MariaDb");
            var serverVersion = MariaDbServerVersion.AutoDetect(connectionString);
            options.UseMySql(connectionString, serverVersion);
        });
    }

    public static void AddConfiguredIdentity(this IServiceCollection services)
    {
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password Policy
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false; // To avoid comlexity UX issues
                options.Password.RequiredLength = 8;

                // Lockout Policy
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.AllowedForNewUsers = true;

                // User Settings
                options.User.RequireUniqueEmail = true;

                // Sign-in Settings
                //options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
    }
}
