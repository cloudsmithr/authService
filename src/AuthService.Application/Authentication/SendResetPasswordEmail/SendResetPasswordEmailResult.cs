namespace AuthService.Application.Authentication.SendResetPasswordEmail;

public class SendResetPasswordEmailResult
{
    public SendResetPasswordEmailOutcome Outcome { get; }
    public string? Message { get; }

    public SendResetPasswordEmailResult(SendResetPasswordEmailOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}