using Microsoft.AspNetCore.Mvc;
using TodoApp.API.Database;

namespace TodoApp.API.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsersAsync()
    {
        await dbContext.SaveChangesAsync();
        string[] users = ["Ali", "Nastaran", "Seyed", "Yones"];
        return Ok(users);
    }
}
