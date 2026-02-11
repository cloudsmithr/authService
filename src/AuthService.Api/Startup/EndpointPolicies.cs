using System.Security.Claims;
using System.Threading.RateLimiting;
using AuthService.Infrastructure.Policies;

namespace AuthService.Api.Startup;

public static class EndpointPolicies
{
    private const string GlobalIp = "GlobalIp";
    private const string PerUserRefresh = "PerUserRefresh";

    public static IServiceCollection AddEndpointPolicies(this IServiceCollection services, ConfigurationManager config)
    {
        PolicySettings? policySettings = config.GetSection(nameof(PolicySettings)).Get<PolicySettings>();

        if (policySettings == null)
        {
            throw new NullReferenceException("Policy settings not found");
        }
        
        services.AddRateLimiter(options =>
        {
            // For use on endpoints that don't require authentication
            options.AddPolicy(GlobalIp, context =>
            {
                string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0] 
                            ?? context.Connection.RemoteIpAddress?.ToString() 
                            ?? "unknown";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ip,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policySettings.GlobalIp.PermitLimit,
                        Window      = TimeSpan.FromMinutes(policySettings.GlobalIp.WindowInMinutes),
                        QueueLimit  = policySettings.GlobalIp.QueueLimit
                    });
            });
            
            // For use on endpoints that require authentication and shouldn't be spammed
            options.AddPolicy(PerUserRefresh, context =>
            {
                // extract userId from the JWT
                string userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                             ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policySettings.PerUserRefresh.PermitLimit,
                        Window      = TimeSpan.FromMinutes(policySettings.PerUserRefresh.WindowInMinutes),
                        QueueLimit  = policySettings.PerUserRefresh.QueueLimit
                    });
            });
        });

        return services;
    }
    
    public static IEndpointConventionBuilder RequireGlobalIpRateLimiting(
        this IEndpointConventionBuilder builder)
        => builder.RequireRateLimiting(GlobalIp);    
    
    public static IEndpointConventionBuilder RequirePerUserRefreshRateLimiting(
        this IEndpointConventionBuilder builder)
        => builder.RequireRateLimiting(PerUserRefresh);
}