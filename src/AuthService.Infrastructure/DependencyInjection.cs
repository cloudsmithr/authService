using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultConnKey = "ConnectionStrings:Default";

    public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration config)
    {
        string? cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? config[DefaultConnKey];
        
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string not configured. Set ConnectionStrings:Default.");

        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseSqlServer(cs, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });
        
        return services;
    }
}