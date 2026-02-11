using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


namespace AuthService.Api.Startup;

public static class AuthenticationSettings
{
    public static IServiceCollection AddApiAuthenticationSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    
                    // TODO: Have this value read from KeyVault instead of appsettings
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["InfrastructureSettings:SecuritySettings:JwtSettings:Key"]!)),
                    ValidIssuer = configuration["InfrastructureSettings:SecuritySettings:JwtSettings:Issuer"]!,
                    ValidAudience = configuration["InfrastructureSettings:SecuritySettings:JwtSettings:Audience"]!,
                    ClockSkew = TimeSpan.Zero,
                    
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Allow expired tokens for the refresh endpoint
                        if (context.Request.Path.StartsWithSegments("/api/auth/refreshToken"))
                        {
                            context.Options.TokenValidationParameters.ValidateLifetime = false;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}