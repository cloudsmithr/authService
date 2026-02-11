namespace AuthService.Domain.Entities;

public class RefreshToken
{
    public required Guid Id { get; set; }
    public required string Token { get; set; }
    public required Guid UserId { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}