namespace AuthService.Application.Authentication.ResetPassword;

public class ResetPasswordRequest
{
    public required string VerificationToken { get; set; }
    public required string Password { get; set; }
}