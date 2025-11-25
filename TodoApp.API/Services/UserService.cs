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
        var user = await GetByIdAsync(userId);

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
        var user = await GetByIdAsync(id);
        if (user is null)
            return;

        await userManager.DeleteAsync(user);
    }

    public async Task<ApplicationUserDto?> GetDtoByIdAsync(string id, bool includeDeleted = false)
    {
        var query = dbContext.Users.AsQueryable();

        if (!includeDeleted)
            query = query.Where(u => !u.IsDeleted);

        return await query
            .Select(u => new ApplicationUserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                UserName = u.UserName,
                Email = u.Email,
            })
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        var query = dbContext.Users.AsQueryable();

        if (!includeDeleted)
            query = query.Where(u => !u.IsDeleted);

        return await query.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<ApplicationUser>> GetUsersAsync(int page, int pageSize)
    {
        return await dbContext
            .Users.OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<ApplicationUserDto>> GetUserDtosAsync(int page, int pageSize)
    {
        return await dbContext
            .Users.OrderBy(u => u.Email)
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
        var user = await GetByIdAsync(userId);

        if (user is null)
            return;

        user.IsDeleted = false;
        await userManager.UpdateAsync(user);
    }

    public async Task RestoreUserAsync(ApplicationUser user)
    {
        if (user is null)
            return;

        user.IsDeleted = false;
        await userManager.UpdateAsync(user);
    }

    public async Task SoftDeleteAsync(string userId)
    {
        var user = await GetByIdAsync(userId);

        if (user is null)
            return;

        user.IsDeleted = true;
        await userManager.UpdateAsync(user);
    }

    public async Task SoftDeleteAsync(ApplicationUser user)
    {
        if (user is null)
            return;

        user.IsDeleted = true;
        await userManager.UpdateAsync(user);
    }

    public async Task UpdateProfileAsync(ApplicationUser user, ApplicationUserUpdateDto updateDto)
    {
        user.FirstName = updateDto.FirstName;
        user.LastName = updateDto.LastName ?? string.Empty;

        await userManager.UpdateAsync(user);
    }

    public async Task UpdateProfileAsync(string userId, ApplicationUserUpdateDto updateDto)
    {
        var user = await GetByIdAsync(userId);

        if (user is null)
            return;

        await UpdateProfileAsync(user, updateDto);
    }
}
