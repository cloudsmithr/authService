using AuthService.Application.Authentication.SendResetPasswordEmail;

namespace AuthService.Api.Endpoints.Authentication;

public static class SendResetPasswordEmailHttp
{
    public static IResult ToHttp(this SendResetPasswordEmailResult result)
    {
        return result.Outcome switch
        {
            SendResetPasswordEmailOutcome.Success => Results.Ok(),
            SendResetPasswordEmailOutcome.BadRequest => Results.BadRequest(new { message = result.Message ?? "Invalid input" }),
            SendResetPasswordEmailOutcome.EmailNotFound => Results.Ok(),
            _ => Results.Problem(result.Message ?? "Sending password reset email failed. Please try again later.", statusCode: 500)
        };
    }
}