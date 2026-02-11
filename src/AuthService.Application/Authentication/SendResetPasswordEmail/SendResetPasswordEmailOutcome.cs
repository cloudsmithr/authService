namespace AuthService.Application.Authentication.SendResetPasswordEmail;

public enum SendResetPasswordEmailOutcome
{
    Success,
    BadRequest,
    EmailNotFound,
}