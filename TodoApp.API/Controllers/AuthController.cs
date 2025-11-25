using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TodoApp.API.DTOs.ApplicationUser;
using TodoApp.API.Entities;
using TodoApp.API.Services;

namespace TodoApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(ApplicationUserRegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await authService.RegisterAsync(registerDto);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Created();
    }
}
