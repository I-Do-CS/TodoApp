using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoApp.API.Entities;

namespace TodoApp.API.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Enforce Unique email at DB level
        builder.Entity<ApplicationUser>().HasIndex(u => u.Email).IsUnique();

        builder.Entity<RefreshToken>().HasIndex(r => r.TokenHash);
    }
}
