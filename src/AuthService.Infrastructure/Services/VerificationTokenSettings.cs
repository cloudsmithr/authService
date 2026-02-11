namespace AuthService.Infrastructure.Services;

public class VerificationTokenSettings
{
    public required int TokenSize { get; set; }
    public required string SecretKey { get; set; }
}