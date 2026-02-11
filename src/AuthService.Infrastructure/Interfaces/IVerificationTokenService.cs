namespace AuthService.Infrastructure.Interfaces;

public interface IVerificationTokenService
{
    string GenerateVerificationToken();
    string HashToken(string token, string context = "");
}