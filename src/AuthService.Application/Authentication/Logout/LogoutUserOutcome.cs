namespace AuthService.Application.Authentication.Logout;

public enum LogoutUserOutcome
{
    SuccessfullyRevoked,
    AlreadyRevoked,
    Expired,
    NotFound,
    BadRequest
}