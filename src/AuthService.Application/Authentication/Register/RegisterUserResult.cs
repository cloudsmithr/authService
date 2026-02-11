namespace AuthService.Application.Authentication.Register;

public class RegisterUserResult
{
    public RegisterUserOutcome Outcome { get; }
    public string? Message { get; }

    public RegisterUserResult(RegisterUserOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}