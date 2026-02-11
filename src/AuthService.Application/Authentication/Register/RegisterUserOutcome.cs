namespace AuthService.Application.Authentication.Register;

public enum RegisterUserOutcome
{
    Success,
    BadRequest,
    EmailAlreadyExists,
    EmailExistsButEmailNotVerified
}