using System.Security.Cryptography;
using System.Text;
using AuthService.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Services;

public class VerificationTokenService : IVerificationTokenService
{
    private readonly VerificationTokenSettings _settings;
    
    public VerificationTokenService(IOptions<VerificationTokenSettings> settings)
    {
        _settings = settings.Value;
    }
    
    public string GenerateVerificationToken()
    {
        var tokenBytes = new byte[_settings.TokenSize];
        RandomNumberGenerator.Fill(tokenBytes);
        
        return Convert.ToBase64String(tokenBytes);
    }
    
    public string HashToken(string token, string context)
    {
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Context is required", nameof(context));
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.SecretKey));

        string input = $"{context}:{token}";
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));

        return Convert.ToBase64String(hashBytes);
    }
}