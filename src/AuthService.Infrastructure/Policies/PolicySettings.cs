namespace AuthService.Infrastructure.Policies;

public class PolicySettings
{
    public required RateLimitPolicy GlobalIp { get; set; }
    public required RateLimitPolicy PerUserRefresh { get; set; }
}