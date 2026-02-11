using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    
    private string _email = string.Empty;
    
    [MaxLength(256)]
    public required string Email 
    { 
        get => _email;
        set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;
    [MaxLength(256)]
    public string? AvatarUrl  { get; set; }
    [MaxLength(256)]
    public required string PasswordHash { get; set; }
    [MaxLength(256)]
    public required string PasswordSalt { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}