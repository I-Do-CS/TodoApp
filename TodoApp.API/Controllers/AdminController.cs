using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.API.Services;

namespace TodoApp.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(AuthService authService) : ControllerBase
{
    // POST api/admin/promote/{userId}
    [HttpPost("promote/{userId}")]
    public async Task<IActionResult> Promote(string userId)
    {
        var result = await authService.PromoteToAdminAsync(userId);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }

    // POST api/admin/demote/{userId}
    [HttpPost("demote/{userId}")]
    public async Task<IActionResult> Demote(string userId)
    {
        var result = await authService.DemoteFromAdminAsync(userId);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }
}
