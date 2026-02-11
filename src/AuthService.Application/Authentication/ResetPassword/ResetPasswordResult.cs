namespace AuthService.Application.Authentication.ResetPassword;

public class ResetPasswordResult
{
    public ResetPasswordOutcome Outcome { get; }
    public string? Message { get; }

    public ResetPasswordResult(ResetPasswordOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}