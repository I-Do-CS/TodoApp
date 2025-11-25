using Microsoft.AspNetCore.Identity;

namespace TodoApp.API.DTOs.ApplicationUser;

public record ApplicationUserDto
{
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? UserName {  get; init; } = string.Empty;
    public string? Email {  get; init; } = string.Empty;
}
