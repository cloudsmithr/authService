using AuthService.Application.Authentication.Login;

namespace AuthService.Application.Authentication.VerifyEmail;

// Our Verify Email Response must always include the LoginUserResponse information for automatic login
public class VerifyEmailResponse : LoginUserResponse
{
}