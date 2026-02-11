namespace AuthService.Application.Authentication.Login;

public enum LoginUserOutcome
{
    Success,
    BadRequest,
    UsernameNotFound,
    InvalidPassword,
    EmailNotVerified,
    ServerError
}