namespace AuthService.Application.Authentication.Register;

public class RegisterUserSettings
{
    public required int MinimumDurationMs { get; set; }
    public required int MinimumPasswordLength { get; set; } 
}