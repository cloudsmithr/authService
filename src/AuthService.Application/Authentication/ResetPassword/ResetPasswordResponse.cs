namespace AuthService.Application.Authentication.ResetPassword;

public class ResetPasswordResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}