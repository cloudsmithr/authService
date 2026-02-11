namespace AuthService.Application.Authentication.RefreshToken;

public class RefreshTokenResponse
{
    public required string NewToken { get; set; }
    public required string RefreshToken { get; set; }
}