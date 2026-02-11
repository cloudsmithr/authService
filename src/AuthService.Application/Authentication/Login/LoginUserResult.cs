using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.Login;

public sealed class LoginUserResult
{
    public LoginUserOutcome Outcome { get; }
    public ApiToken? AccessToken { get; }
    public ApiToken? RefreshToken { get; }
    public string? Message { get; }

    public LoginUserResult(LoginUserOutcome outcome, ApiToken? accessToken = null, ApiToken? refreshToken = null, string? message = null)
    {
        Outcome = outcome;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Message = message;
    }
}