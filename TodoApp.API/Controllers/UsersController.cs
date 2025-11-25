using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.API.DTOs.ApplicationUser;
using TodoApp.API.Services;
using TodoApp.API.Shared;

namespace TodoApp.API.Controllers;

[ApiController]
//[Authorize]
[Route("api/users")]
public sealed class UsersController(UserService userService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationUserDto>> GetById(string id)
    {
        var userDto = await userService.GetDtoByIdAsync(id);
        if (userDto is null)
        {
            return NotFound();
        }

        return Ok(userDto);
    }

    [HttpPut("{id}/update-profile")]
    public async Task<IActionResult> UpdateProfile(
        string id,
        [FromBody] ApplicationUserUpdateDto updateDto
    )
    {
        await userService.UpdateProfileAsync(id, updateDto);

        return NoContent();
    }

    [HttpPut("{id}/change-password")]
    public async Task<IActionResult> ChangePassword(
        string id,
        [FromBody] ApplicationUserChangePasswordDto changePasswordDto
    )
    {
        var result = await userService.ChangePasswordAsync(id, changePasswordDto);
        if (!result.Succeeded)
        {
            return UnprocessableEntity(result.Errors);
        }

        return NoContent();
    }

    // Admin Only
    [HttpGet]
    //[Authorize(Roles = ApplicationUserRoles.Admin)]
    public async Task<IActionResult> GetAll(int pageNumber = 1, int pageSize = 20)
    {
        var userDtos = await userService.GetUserDtosAsync(pageNumber, pageSize);
        return Ok(userDtos);
    }

    [HttpPut("{id}/delete")]
    [Authorize(Roles = ApplicationUserRoles.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        await userService.SoftDeleteAsync(user);
        return NoContent();
    }

    [HttpPut("{id}/restore")]
    [Authorize(Roles = ApplicationUserRoles.Admin)]
    public async Task<IActionResult> Restore(string id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        await userService.RestoreUserAsync(user);
        return NoContent();
    }
}
