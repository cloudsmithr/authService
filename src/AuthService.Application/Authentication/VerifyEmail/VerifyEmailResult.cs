using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.VerifyEmail;

public class VerifyEmailResult
{
    public VerifyEmailOutcome Outcome { get; }
    public ApiToken? AccessToken { get; }
    public ApiToken? RefreshToken { get; }
    public string? Message { get; }

    public VerifyEmailResult(VerifyEmailOutcome outcome, ApiToken? accessToken = null, ApiToken? refreshToken = null, string? message = null)
    {
        Outcome = outcome;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Message = message;
    }
}