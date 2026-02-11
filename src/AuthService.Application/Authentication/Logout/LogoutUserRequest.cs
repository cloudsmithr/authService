namespace AuthService.Application.Authentication.Logout;

public class LogoutUserRequest
{
    public required string RefreshTokenToRevoke { get; set; }
}