namespace AuthService.Application.Authentication.Login;

public class LoginUserResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}