using AuthService.Api.Endpoints.Authentication.Utils;
using AuthService.Application.Authentication.RefreshToken;

namespace AuthService.Api.Endpoints.Authentication;

public static class RefreshTokenHttp
{
    public static IResult ToHttp(this RefreshTokenResult result, HttpContext httpContext)
    {
        return result.Outcome switch
        {
            RefreshTokenOutcome.Success when AuthUtils.ValidateApiToken(result.AccessToken) && AuthUtils.ValidateApiToken(result.RefreshToken)
                => AuthUtils.SetAuthCookiesAndReturn(httpContext, result.AccessToken!, result.RefreshToken!),
            RefreshTokenOutcome.Success 
                => Results.Problem("Token generation failed", statusCode: 500),
            RefreshTokenOutcome.BadRequest => Results.Unauthorized(),
            RefreshTokenOutcome.TokenNotFound => Results.Unauthorized(),
            RefreshTokenOutcome.TokenExpired => Results.Unauthorized(),
            RefreshTokenOutcome.TokenRevoked => Results.Unauthorized(),
            RefreshTokenOutcome.UserNotFound => Results.Unauthorized(),
            _ => Results.Problem(result.Message ?? "Token refresh failed", statusCode: 500)
        };
    }
}