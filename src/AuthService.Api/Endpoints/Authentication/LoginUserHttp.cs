using AuthService.Api.Endpoints.Authentication.Utils;
using AuthService.Application.Authentication.Login;
using AuthService.Application;
using AuthService.Infrastructure.Services;

namespace AuthService.Api.Endpoints.Authentication;

public static class LoginUserHttp
{
    public static IResult ToHttp(this LoginUserResult result, HttpContext httpContext)
    {
        return result.Outcome switch
        {
            LoginUserOutcome.Success when AuthUtils.ValidateApiToken(result.AccessToken) && AuthUtils.ValidateApiToken(result.RefreshToken)
                => AuthUtils.SetAuthCookiesAndReturn(httpContext, result.AccessToken!, result.RefreshToken!),
            LoginUserOutcome.Success 
                => Results.Problem("Token generation failed", statusCode: 500),
            LoginUserOutcome.BadRequest     => Results.BadRequest(new { message = result.Message ?? "invalid input" }),
            LoginUserOutcome.EmailNotVerified => Results.Ok(new GenericErrorResponse() { Error = "email_not_verified", Message = "Your email has not been verified yet. A verification link has been sent to your email, which will automatically log you in." }),
            // collapse all auth failures to 401 to avoid username enumeration
            LoginUserOutcome.UsernameNotFound => Results.Unauthorized(),
            LoginUserOutcome.InvalidPassword  => Results.Unauthorized(),
            _                                 => Results.Problem(result.Message ?? "login failed", statusCode: 500)
        };
    }
}