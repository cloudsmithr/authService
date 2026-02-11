namespace AuthService.Application.Authentication.Logout;

public class LogoutUserResult
{
    public LogoutUserOutcome Outcome { get; }
    public string? Message { get; }

    public LogoutUserResult(LogoutUserOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}