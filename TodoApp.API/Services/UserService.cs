using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApp.API.Database;
using TodoApp.API.DTOs.ApplicationUser;
using TodoApp.API.Entities;

namespace TodoApp.API.Services;

public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext
)
{
    public async Task<IdentityResult> ChangePasswordAsync(
        ApplicationUser user,
        ApplicationUserChangePasswordDto changePasswordDto
    )
    {
        // Use UserManager for identity-specific change-password semantics.
        return await userManager.ChangePasswordAsync(
            user,
            changePasswordDto.CurrentPassword,
            changePasswordDto.NewPassword
        );
    }

    public async Task<IdentityResult> ChangePasswordAsync(
        string userId,
        ApplicationUserChangePasswordDto changePasswordDto
    )
    {
        var user = await GetByIdAsync(userId, includeDeleted: true);

        if (user is null)
        {
            return IdentityResult.Failed(
                [
                    new IdentityError
                    {
                        Code = "NotFound",
                        Description = $"No user with the id of {userId} was found.",
                    },
                ]
            );
        }

        return await ChangePasswordAsync(user, changePasswordDto);
    }

    public async Task DeleteAsync(string id)
    {
        var user = await GetByIdAsync(id, includeDeleted: true);
        if (user is null)
            return;

        await userManager.DeleteAsync(user);
    }

    public async Task<ApplicationUserDto?> GetDtoByIdAsync(string id, bool includeDeleted = false)
    {
        var query = dbContext.Users.AsQueryable().AsNoTracking();

        if (!includeDeleted)
            query = query.Where(u => !u.IsDeleted);

        return await query
            .Where(u => u.Id == id)
            .Select(u => new ApplicationUserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                UserName = u.UserName,
                Email = u.Email,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        var query = dbContext.Users.AsQueryable().AsNoTracking();

        if (!includeDeleted)
            query = query.Where(u => !u.IsDeleted);

        return await query.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<ApplicationUser>> GetUsersAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        return await dbContext
            .Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .ThenBy(u => u.Id) // stable ordering for paging
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<ApplicationUserDto>> GetUserDtosAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        return await dbContext
            .Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .ThenBy(u => u.Id) // stable ordering for paging
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new ApplicationUserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                UserName = u.UserName,
                Email = u.Email,
            })
            .ToListAsync();
    }

    public async Task RestoreUserAsync(string userId)
    {
        var user = await GetByIdAsync(userId, includeDeleted: true);

        if (user is null)
            return;

        user.IsDeleted = false;
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task RestoreUserAsync(ApplicationUser user)
    {
        if (user is null)
            return;

        user.IsDeleted = false;
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(string userId)
    {
        var user = await GetByIdAsync(userId, includeDeleted: true);

        if (user is null)
            return;

        user.IsDeleted = true;
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(ApplicationUser user)
    {
        if (user is null)
            return;

        user.IsDeleted = true;
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(ApplicationUser user, ApplicationUserUpdateDto updateDto)
    {
        user.FirstName = updateDto.FirstName;
        user.LastName = updateDto.LastName ?? string.Empty;

        // Use DbContext for a simple profile update to avoid extra UserManager overhead.
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(string userId, ApplicationUserUpdateDto updateDto)
    {
        var user = await GetByIdAsync(userId, includeDeleted: false);

        if (user is null)
            return;

        await UpdateProfileAsync(user, updateDto);
    }
}
