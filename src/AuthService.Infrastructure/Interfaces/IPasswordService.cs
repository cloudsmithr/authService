namespace AuthService.Infrastructure.Interfaces;

public interface IPasswordService
{
    Task<(string hash, string salt)> HashPassword(string password);

    Task<string> ComputeHashBase64(
        string password,
        string saltBase64);

    Task<bool> Verify(
        string password,
        string saltBase64,
        string expectedHashBase64);
}