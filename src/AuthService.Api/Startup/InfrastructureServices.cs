using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Services;

namespace AuthService.Api.Startup;

public static class InfrastructureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IJwtService, JwtService>();
        services.AddJwtSettings();

        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddPasswordSettings();

        services.AddSingleton<IVerificationTokenService, VerificationTokenService>();
        services.AddVerificationTokenServiceSettings();
        
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddRefreshTokenSettings();
        
        services.AddScoped<IEmailService, EmailService>();
        services.AddEmailSettings();
        
        return services;
    }
    
        private static IServiceCollection AddPasswordSettings(this IServiceCollection services)
    {
        services
            .AddOptions<PasswordSettings>()
            .BindConfiguration("InfrastructureSettings:SecuritySettings:PasswordSettings")
            .ValidateDataAnnotations()
            .Validate(settings => settings.Iterations > 0, "PasswordSettings Iterations must be greater than zero. Application will now exit.")
            .Validate(settings => settings.MemorySizeKb > 0, "PasswordSettings Iterations must be greater than zero. Application will now exit.")
            .Validate(settings => settings.DegreeOfParallelism > 0, "PasswordSettings Iterations must be greater than zero. Application will now exit.")
            .Validate(settings => settings.HashLength > 0, "PasswordSettings HashLength must be greater than zero. Application will now exit.")
            .Validate(settings => settings.SaltLength > 0, "PasswordSettings SaltLength must be greater than zero. Application will now exit.")
            .ValidateOnStart();

        return services;
    }
    
    private static IServiceCollection AddJwtSettings(this IServiceCollection services)
    {
        // TODO: Make sure Key is able to come from KeyVault instead of appsettings
        services
            .AddOptions<JwtSettings>()
            .BindConfiguration("InfrastructureSettings:SecuritySettings:JwtSettings")
            .ValidateDataAnnotations()
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Key), "JWTSettings Key is required. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JWTSettings Issuer is required. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JWTSettings Audience is required. Application will now exit.")
            .Validate(settings => settings.ExpiresInMinutes > 0, "JWTSettings ExpiresInMinutes must be greater than zero. Application will now exit.")
            .ValidateOnStart();

        return services;
    }
    
    public static IServiceCollection AddRefreshTokenSettings(this IServiceCollection services)
    {
        services
            .AddOptions<RefreshTokenSettings>()
            .BindConfiguration("InfrastructureSettings:SecuritySettings:RefreshTokenSettings")
            .ValidateDataAnnotations()
            .Validate(settings => settings.RefreshTokenLength > 0, "RefreshTokenSettings RefreshTokenLength must be greater than zero. Application will now exit.")
            .Validate(settings => settings.RefreshTokenLifeTimeInHours > 0, "RefreshTokenSettings RefreshTokenLifeTimeInHours must be greater than zero. Application will now exit.")
            .Validate(settings => settings.RefreshTokenPurgeCutoffInDays > 0, "RefreshTokenSettings RefreshTokenPurgeCutoffInDays must be greater than zero. Application will now exit.")
            .ValidateOnStart();
        
        return services;
    }
    
    public static IServiceCollection AddEmailSettings(this IServiceCollection services)
    {
        services
            .AddOptions<EmailSettings>()
            .BindConfiguration("InfrastructureSettings:EmailSettings")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Host), "EmailSettings Host is required. Application will now exit.")
            .Validate(settings => settings.Port > 0, "EmailSettings Port must be greater than zero. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromName), "EmailSettings FromName is required. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromEmail), "EmailSettings FromEmail is required. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.BaseUrl), "EmailSettings BaseUrl is required. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.AppName), "EmailSettings AppName is required. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
    
    public static IServiceCollection AddVerificationTokenServiceSettings(this IServiceCollection services)
    {
        services
            .AddOptions<VerificationTokenSettings>()
            .BindConfiguration("InfrastructureSettings:VerificationTokenSettings")
            .Validate(settings => settings.TokenSize > 0, "VerificationTokenSettings TokenSize must be greater than zero. Application will now exit.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.SecretKey), "VerificationTokenSettings SecretKey is required. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}