using AuthService.Application.Authentication.Register;

namespace AuthService.Api.Endpoints.Authentication;

public static class RegisterUserHttp
{
    public static IResult ToHttp(this RegisterUserResult result)
    {
        return result.Outcome switch
        {
            RegisterUserOutcome.Success => Results.Ok(new { message = "User registered successfully" }),
            RegisterUserOutcome.BadRequest => Results.BadRequest(new { message = result.Message ?? "Invalid input" }),
            RegisterUserOutcome.EmailAlreadyExists => Results.Conflict(new { message = "Email already exists" }),
            RegisterUserOutcome.EmailExistsButEmailNotVerified => Results.Conflict(new { message = result.Message ?? "User already exists, but email isn't verified. A new verification email has been sent, please check your inbox and spam folders." }),
            _ => Results.Problem(result.Message ?? "Registration failed", statusCode: 500)
        };
    }
}