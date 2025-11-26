using Microsoft.AspNetCore.Identity;
using TodoApp.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure DbContext and Identity
builder.Services.AddConfiguredDbContext(builder.Configuration);
builder.Services.AddConfiguredIdentity();
builder.Services.AddRuntimeServices();
builder.Services.AddJwtAuthAndPolicies(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Apply DB migrations in development
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var roleManager = services.GetRequiredService<RoleManager<TodoApp.API.Entities.ApplicationRole>>();
var userManager = services.GetRequiredService<UserManager<TodoApp.API.Entities.ApplicationUser>>();
var configuration = services.GetRequiredService<IConfiguration>();
var logger = services.GetRequiredService<ILogger<Program>>();

// Ensure standard roles exist
var roles = new[] { "User", "Admin" };
foreach (var r in roles)
{
    if (!await roleManager.RoleExistsAsync(r))
    {
        var createResult = await roleManager.CreateAsync(
            new TodoApp.API.Entities.ApplicationRole { Name = r }
        );
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Failed to create role {Role}: {Errors}",
                r,
                string.Join(", ", createResult.Errors.Select(e => e.Description))
            );
        }
    }
}

// Optional: seed an initial admin if configured. Use secure config sources (env vars / Key Vault) in production.
var adminEmail = configuration["InitialAdmin:Email"];
var adminPassword = configuration["InitialAdmin:Password"];
if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
{
    var admin = await userManager.FindByEmailAsync(adminEmail);
    if (admin is null)
    {
        admin = new TodoApp.API.Entities.ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
        };
        var create = await userManager.CreateAsync(admin, adminPassword);
        if (create.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Seeded initial admin user: {Email}", adminEmail);
        }
        else
        {
            logger.LogError(
                "Failed to create initial admin {Email}: {Errors}",
                adminEmail,
                string.Join(", ", create.Errors.Select(e => e.Description))
            );
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}

await app.RunAsync();
