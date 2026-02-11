namespace AuthService.Infrastructure.Policies;

public class RateLimitPolicy
{
    public required int PermitLimit { get; set; }
    public required int WindowInMinutes { get; set; }
    public required int QueueLimit { get; set; }    
}