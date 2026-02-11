using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }
    public ApiToken GenerateToken(Guid userId, string email)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        
        if (!EmailUtils.IsValidEmail(email))
        {
            throw new ArgumentException("Invalid email address", nameof(email));
        }
        
        Claim[] claims = new []
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        SigningCredentials creds = new (
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            SecurityAlgorithms.HmacSha256);

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes);
        
        JwtSecurityToken token = new (
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds
        );

        string returnToken = new JwtSecurityTokenHandler().WriteToken(token);
        
        if (string.IsNullOrWhiteSpace(returnToken))
            throw new InvalidOperationException("Token Generation did not produce a valid JWT.");

        return new ApiToken(returnToken, expiresAt);
    }
}