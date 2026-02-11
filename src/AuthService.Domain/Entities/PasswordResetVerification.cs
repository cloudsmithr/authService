namespace AuthService.Domain.Entities;

public class PasswordResetVerification
{
    public required Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string HashedToken { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
}