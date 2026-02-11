using AuthService.Application.Authentication.ResetPassword;

namespace AuthService.Api.Endpoints.Authentication;

public static class ResetPasswordHttp
{
    public static IResult ToHttp(this ResetPasswordResult result)
    {
        return result.Outcome switch
        {
            ResetPasswordOutcome.Success => Results.Ok(),
            ResetPasswordOutcome.BadRequest => Results.BadRequest(new { message = result.Message ?? "Invalid input" }),
            ResetPasswordOutcome.LinkExpired => Results.BadRequest(new { message = "Verification link expired." }),
            ResetPasswordOutcome.LinkNotFound => Results.BadRequest(new { message = "Invalid verification link" }),
            ResetPasswordOutcome.LinkInvalid => Results.BadRequest(new { message = "Invalid verification link" }),
            _ => Results.Problem(result.Message ?? "Sending password reset email failed. Please try again later.", statusCode: 500)
        };
    }
}