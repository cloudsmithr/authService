using AuthService.Application.Authentication.Login;
using AuthService.Application.Authentication.Logout;
using AuthService.Application.Authentication.RefreshToken;
using AuthService.Application.Authentication.Register;
using AuthService.Application.Authentication.ResendEmailVerification;
using AuthService.Application.Authentication.ResetPassword;
using AuthService.Application.Authentication.SendResetPasswordEmail;
using AuthService.Application.Authentication.VerifyEmail;

namespace AuthService.Api.Startup;

public static class ApplicationHandlers
{
    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();
        services.AddRegisterUserSettings();

        services.AddScoped<LoginUserHandler>();
        services.AddLoginUserSettings();
        
        services.AddScoped<RefreshTokenHandler>();
        // RefreshToken endpoint has no settings currently, as those settings are handled by the RefreshTokenService
        
        services.AddScoped<LogoutUserHandler>();
        // Logout user endpoint has no settings currently, as they aren't needed.
        
        services.AddScoped<ResendEmailVerificationHandler>();
        services.AddResendEmailVerificationSettings();
        
        services.AddScoped<VerifyEmailHandler>();
        services.AddVerifyEmailSettings();
        
        services.AddScoped<ResetPasswordHandler>();
        services.AddResetPasswordSettings();
        
        services.AddScoped<SendResetPasswordEmailHandler>();
        services.AddSendResetPasswordEmailSettingsSettings();
        
        return services;
    }
    
    private static IServiceCollection AddRegisterUserSettings(this IServiceCollection services)
    {
        services
            .AddOptions<RegisterUserSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:RegisterUserSettings")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
    
    private static IServiceCollection AddLoginUserSettings(this IServiceCollection services)
    {
        services
            .AddOptions<LoginUserSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:LoginUserSettings")
            .Validate(settings => settings.MinimumDurationMs > 0, "LoginUserSettings MinimumDurationMs must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }

    private static IServiceCollection AddResendEmailVerificationSettings(this IServiceCollection services)
    {
        services
            .AddOptions<ResendEmailVerificationSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:ResendEmailVerificationSettings")
            .Validate(settings => settings.MinimumDurationMs > 0, "ResendEmailVerificationSettings MinimumDurationMs must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
    
    private static IServiceCollection AddVerifyEmailSettings(this IServiceCollection services)
    {
        services
            .AddOptions<VerifyEmailSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:VerifyEmailSettings")
            .Validate(settings => settings.MinimumDurationMs > 0, "VerifyEmailSettings MinimumDurationMs must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddResetPasswordSettings(this IServiceCollection services)
    {
        services
            .AddOptions<ResetPasswordSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:ResetPasswordSettings")
            .Validate(settings => settings.MinimumDurationMs > 0, "ResetPasswordSettings MinimumDurationMs must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
    
    private static IServiceCollection AddSendResetPasswordEmailSettingsSettings(this IServiceCollection services)
    {
        services
            .AddOptions<SendResetPasswordEmailSettings>()
            .BindConfiguration("ApplicationSettings:HandlerSettings:SendResetPasswordEmailSettings")
            .Validate(settings => settings.MinimumDurationMs > 0, "SendResetPasswordEmailSettings MinimumDurationMs must be greater than zero. Application will now exit.")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}