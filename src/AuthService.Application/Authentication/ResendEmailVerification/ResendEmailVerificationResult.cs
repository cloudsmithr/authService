namespace AuthService.Application.Authentication.ResendEmailVerification;

public class ResendEmailVerificationResult
{
    public ResendEmailVerificationOutcome Outcome { get; }
    public string? Message { get; }

    public ResendEmailVerificationResult(ResendEmailVerificationOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}