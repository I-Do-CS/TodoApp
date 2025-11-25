using Microsoft.EntityFrameworkCore;
using TodoApp.API.Database;

namespace TodoApp.API.Extensions;

public static class ApplicationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();

        await using ApplicationDbContext applicationDbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await applicationDbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Application database migration applied successfully");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error ocurred while applying database migrations");
            throw;
        }
    }
}
