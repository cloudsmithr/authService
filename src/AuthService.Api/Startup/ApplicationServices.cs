using AuthService.Application.Interfaces;
using AuthService.Application.Services.EmailVerification;
using AuthService.Application.Services.ResetPassword;

namespace AuthService.Api.Startup;

public static class ApplicationServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddEmailVerificationSettings();
        
        services.AddScoped<IResetPasswordService, ResetPasswordService>();
        services.AddResetPasswordServiceSettings();
        
        return services;
    }
    
    private static IServiceCollection AddEmailVerificationSettings(this IServiceCollection services)
    {
        services
            .AddOptions<EmailVerificationSettings>()
            .BindConfiguration("ApplicationSettings:ServiceSettings:EmailVerificationSettings")
            .Validate(settings => settings.HoursToLive > 0, "EmailVerificationSettings HoursToLive must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
    
    private static IServiceCollection AddResetPasswordServiceSettings(this IServiceCollection services)
    {
        services
            .AddOptions<ResetPasswordServiceSettings>()
            .BindConfiguration("ApplicationSettings:ServiceSettings:ResetPasswordServiceSettings")
            .Validate(settings => settings.HoursToLive > 0, "ResetPasswordServiceSettings HoursToLive must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
}