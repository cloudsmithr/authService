using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using AuthService.Infrastructure.Interfaces;

namespace AuthService.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    private readonly PasswordSettings _passwordSettings;

    public PasswordService(IOptions<PasswordSettings> passwordSettings)
    {
        _passwordSettings = passwordSettings.Value;
    }
    
    public async Task<(string hash, string salt)> HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        byte[] saltBytes = RandomNumberGenerator.GetBytes(_passwordSettings.SaltLength);
        string saltBase64 = Convert.ToBase64String(saltBytes);
        
        using Argon2id argon2 = new (Encoding.UTF8.GetBytes(password))
        {
            Salt = saltBytes,
            DegreeOfParallelism = _passwordSettings.DegreeOfParallelism,
            MemorySize = _passwordSettings.MemorySizeKb,
            Iterations = _passwordSettings.Iterations
        };
        
        byte[] hashBytes = await argon2.GetBytesAsync(_passwordSettings.HashLength);
        string hash = Convert.ToBase64String(hashBytes);
        
        if (string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("Password hashing failed to produce valid hash.");

        if (string.IsNullOrWhiteSpace(saltBase64))
            throw new InvalidOperationException("Password hashing failed to produce valid salt.");
        
        return (hash, saltBase64);
    }
    
    public async Task<string> ComputeHashBase64(
        string password,
        string saltBase64)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        if (string.IsNullOrEmpty(saltBase64))
            throw new ArgumentException("Salt cannot be null or empty", nameof(saltBase64));

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] saltBytes = Convert.FromBase64String(saltBase64);

        using Argon2id hasher = new (passwordBytes)
        {
            Salt = saltBytes,
            Iterations = _passwordSettings.Iterations,
            MemorySize = _passwordSettings.MemorySizeKb,
            DegreeOfParallelism = _passwordSettings.DegreeOfParallelism
        };

        byte[] hashBytes = await hasher.GetBytesAsync(_passwordSettings.HashLength);
        return Convert.ToBase64String(hashBytes);
    }
    
    public async Task<bool> Verify(
        string password,
        string saltBase64,
        string expectedHashBase64)
    {
        string computedHashBase64 = await ComputeHashBase64(password, saltBase64);
        byte[] expected = Convert.FromBase64String(expectedHashBase64);
        byte[] actual = Convert.FromBase64String(computedHashBase64);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}