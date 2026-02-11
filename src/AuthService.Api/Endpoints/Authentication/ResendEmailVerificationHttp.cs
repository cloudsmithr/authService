using AuthService.Application.Authentication.ResendEmailVerification;

namespace AuthService.Api.Endpoints.Authentication;

public static class ResendEmailVerificationHttp
{
    public static IResult ToHttp(this ResendEmailVerificationResult result)
    {
        return result.Outcome switch
        {
            ResendEmailVerificationOutcome.Success => Results.Ok(),
            ResendEmailVerificationOutcome.BadRequest => Results.BadRequest(new { message = result.Message ?? "Invalid input" }),
            ResendEmailVerificationOutcome.EmailNotFound => Results.Ok(),
            ResendEmailVerificationOutcome.EmailAlreadyVerified => Results.Ok(),
            _ => Results.Problem(result.Message ?? "Sending verification email failed. Please try again later.", statusCode: 500)
        };
    }    
}