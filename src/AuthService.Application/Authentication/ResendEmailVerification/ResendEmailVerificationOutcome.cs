namespace AuthService.Application.Authentication.ResendEmailVerification;

public enum ResendEmailVerificationOutcome
{
    Success,
    BadRequest,
    EmailNotFound,
    EmailAlreadyVerified
}