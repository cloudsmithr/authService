using AuthService.Infrastructure.Services;

namespace AuthService.Api.Endpoints.Authentication.Utils;

public static class AuthUtils
{
    public static IResult SetAuthCookiesAndReturn(HttpContext httpContext, ApiToken accessToken, ApiToken refreshToken)
    {
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        bool isProduction = env.IsProduction();
        
        // make sure we're not caching this result
        httpContext.Response.Headers.Append("Cache-Control", "no-store");
        httpContext.Response.Headers.Append("Pragma", "no-cache");
        httpContext.Response.Headers.Append("Expires", DateTimeOffset.UtcNow.ToString("R"));
        
        // Set refresh token cookie
        httpContext.Response.Cookies.Append("refreshToken", refreshToken.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,          // Set to false for local development if needed
            SameSite = SameSiteMode.Lax,
            Expires = refreshToken.Expiration, // Match your refresh token expiration
            Path = "/"
        });

        // Return success without tokens in body
        return Results.Ok(new { message = "Login successful", accessToken = accessToken.Token, accessExpiration = accessToken.Expiration });
    }

    public static bool ValidateApiToken(ApiToken token)
    {
        return (token is not null && 
                !string.IsNullOrWhiteSpace(token.Token) &&
                token.Expiration > DateTime.UtcNow);
    }
}