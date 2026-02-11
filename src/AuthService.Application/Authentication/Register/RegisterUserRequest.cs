using AuthService.Application.Abstract;

namespace AuthService.Application.Authentication.Register;

public class RegisterUserRequest : AbstractEmailRequestBase
{
    public required string Password { get; set; }
    public string? Username { get; set; }
}