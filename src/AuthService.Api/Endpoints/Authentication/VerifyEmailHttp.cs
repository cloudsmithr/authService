using AuthService.Api.Endpoints.Authentication.Utils;
using AuthService.Application.Authentication.VerifyEmail;

namespace AuthService.Api.Endpoints.Authentication;

public static class VerifyEmailHttp
{
    public static IResult ToHttp(this VerifyEmailResult result, HttpContext httpContext)
    {
        return result.Outcome switch
        {
            VerifyEmailOutcome.Success when AuthUtils.ValidateApiToken(result.AccessToken) && AuthUtils.ValidateApiToken(result.RefreshToken)
                => AuthUtils.SetAuthCookiesAndReturn(httpContext, result.AccessToken!, result.RefreshToken!),
            VerifyEmailOutcome.Success 
                => Results.Problem("Token generation failed", statusCode: 500),
            VerifyEmailOutcome.BadRequest => Results.BadRequest(new { message = result.Message ?? "Invalid input" }),
            VerifyEmailOutcome.LinkExpired => Results.BadRequest(new { message = "Verification link expired." }),
            VerifyEmailOutcome.LinkNotFound => Results.BadRequest(new { message = "Invalid verification link" }),
            VerifyEmailOutcome.UserNotFound => Results.BadRequest(new { message = "Invalid verification link" }),
            VerifyEmailOutcome.LinkInvalid => Results.BadRequest(new { message = "Invalid verification link" }),
            _ => Results.Problem(result.Message ?? "Email verification failed", statusCode: 500)
        };
    }    
}