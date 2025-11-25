using System.ComponentModel.DataAnnotations;

namespace TodoApp.API.DTOs.ApplicationUser;

public sealed record ApplicationUserChangePasswordDto
{

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;
}
