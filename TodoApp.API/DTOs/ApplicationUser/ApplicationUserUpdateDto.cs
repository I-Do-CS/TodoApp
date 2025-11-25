using System.ComponentModel.DataAnnotations;

namespace TodoApp.API.DTOs.ApplicationUser;

public sealed record ApplicationUserUpdateDto
{
    [Required]
    public string FirstName {  get; init; } = string.Empty;

    public string? LastName {  get; init; } = string.Empty;
};
