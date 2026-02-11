using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.RefreshToken;

public class RefreshTokenResult
{
    public RefreshTokenOutcome Outcome { get; }
    public ApiToken? AccessToken { get; }
    public ApiToken? RefreshToken { get; }
    public string? Message { get; }

    public RefreshTokenResult(RefreshTokenOutcome outcome, ApiToken? accessToken = null, ApiToken? refreshToken = null, string? message = null)
    {
        Outcome = outcome;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Message = message;
    }
}