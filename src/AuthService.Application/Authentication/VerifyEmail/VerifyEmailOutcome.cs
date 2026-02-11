namespace AuthService.Application.Authentication.VerifyEmail;

public enum VerifyEmailOutcome
{
    Success,
    UserNotFound,
    LinkExpired,
    LinkNotFound,
    LinkInvalid,
    BadRequest,
}