namespace AuthService.Application.Authentication.VerifyEmail;

public class VerifyEmailRequest
{
    public required string VerificationToken  { get; set; }
}