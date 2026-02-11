using AuthService.Application.Abstract;

namespace AuthService.Application.Authentication.Login;

public class LoginUserRequest : AbstractEmailRequestBase
{
    public required string Password { get; set; }
}