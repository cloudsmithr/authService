namespace AuthService.Application.Authentication.ResetPassword;

public enum ResetPasswordOutcome
{
    Success,
    BadRequest,
    LinkNotFound,
    LinkExpired,
    LinkInvalid
}