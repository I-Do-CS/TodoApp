using System.ComponentModel.DataAnnotations;

namespace TodoApp.API.DTOs.ApplicationUser;

public sealed record ApplicationUserRegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword {  get; init; } = string.Empty;

    [Required]
    public string FirstName {  get; init; } = string.Empty;

    public string? LastName {  get; init; } = string.Empty;
};
