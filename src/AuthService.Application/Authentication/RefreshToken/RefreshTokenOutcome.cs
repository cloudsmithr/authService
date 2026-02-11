namespace AuthService.Application.Authentication.RefreshToken;

public enum RefreshTokenOutcome
{
    Success,
    BadRequest,
    TokenNotFound,
    TokenExpired,
    TokenRevoked,
    UserNotFound    
}