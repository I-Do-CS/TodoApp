using Microsoft.AspNetCore.Identity;
using TodoApp.API.DTOs.ApplicationUser;
using TodoApp.API.Entities;

namespace TodoApp.API.Services;

public class AuthService(UserManager<ApplicationUser> serviceManager)
{
    public async Task<IdentityResult> RegisterAsync(ApplicationUserRegisterDto registerDto)
    {
        ApplicationUser user = new()
        {
            UserName = registerDto.Email,
            Email = registerDto.Email,
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName ?? string.Empty,
        };

        return await serviceManager.CreateAsync(user, registerDto.Password);
    }
}
