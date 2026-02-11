using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Infrastructure.Services;

public class JwtServiceTests
{
    private readonly JwtSettings _settings;

    public JwtServiceTests()
    {
        _settings = new JwtSettings
        {
            Key = "this-is-a-very-secure-secret-key-for-testing-purposes-at-least-32-characters",
            Issuer = "AuthServiceTestIssuer",
            Audience = "AuthServiceTestAudience",
            ExpiresInMinutes = 60
        };
    }

    [Fact]
    public void GenerateToken_Should_Return_NonEmpty_String()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        ApiToken token = service.GenerateToken(userId, email);

        // Assert
        token.Token.Should().NotBeNullOrEmpty();
        token.Expiration.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_Should_Create_Valid_Jwt_Token()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        ApiToken token = service.GenerateToken(userId, email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var act = () => handler.ReadJwtToken(token.Token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateToken_Should_Include_UserId_In_Sub_Claim()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        ApiToken token = service.GenerateToken(userId, email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token.Token);
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be(userId.ToString());
    }

    [Fact]
    public void GenerateToken_Should_Include_Email_In_UniqueName_Claim()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be(email);
    }

    [Fact]
    public void GenerateToken_Should_Include_Jti_Claim()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        
        jtiClaim.Should().NotBeNull();
        Guid.TryParse(jtiClaim!.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_Should_Generate_Unique_Jti_For_Each_Token()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token1 = service.GenerateToken(userId, email).Token;
        string token2 = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken1 = handler.ReadJwtToken(token1);
        var jwtToken2 = handler.ReadJwtToken(token2);
        
        var jti1 = jwtToken1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwtToken2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        
        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateToken_Should_Set_Correct_Issuer()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be(_settings.Issuer);
    }

    [Fact]
    public void GenerateToken_Should_Set_Correct_Audience()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Audiences.Should().Contain(_settings.Audience);
    }

    [Fact]
    public void GenerateToken_Should_Set_Expiration_Time()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        ApiToken token = service.GenerateToken(userId, email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token.Token);
        
        var expectedExpiry = DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
        expectedExpiry.Should().BeCloseTo(token.Expiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_Should_Use_HmacSha256_Algorithm()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void GenerateToken_Should_Be_Verifiable_With_Secret_Key()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token = service.GenerateToken(userId, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key))
        };

        var act = () => handler.ValidateToken(token, validationParameters, out _);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test")]
    [InlineData("")]
    public void GenerateToken_Should_Throw_ArgumentException_For_Invalid_Email(string invalidEmail)
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();

        // Act
        var act = () => service.GenerateToken(userId, invalidEmail);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid email address*")
            .And.ParamName.Should().Be("email");
    }

    [Fact]
    public void GenerateToken_Should_Work_With_Different_Expiration_Times()
    {
        // Arrange
        var customSettings = new JwtSettings
        {
            Key = _settings.Key,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            ExpiresInMinutes = 120 // 2 hours
        };
        JwtService service = new(Options.Create(customSettings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        ApiToken token = service.GenerateToken(userId, email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token.Token);
        
        var expectedExpiry = DateTime.UtcNow.AddMinutes(120);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
        expectedExpiry.Should().BeCloseTo(token.Expiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_Should_Create_Different_Tokens_For_Same_User()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token1 = service.GenerateToken(userId, email).Token;
        string token2 = service.GenerateToken(userId, email).Token;

        // Assert
        token1.Should().NotBe(token2); // Due to different Jti and timestamps
    }

    [Fact]
    public void GenerateToken_Should_Handle_Different_UserIds()
    {
        // Arrange
        JwtService service = new(Options.Create(_settings));
        Guid userId1 = Guid.NewGuid();
        Guid userId2 = Guid.NewGuid();
        string email = "test@example.com";

        // Act
        string token1 = service.GenerateToken(userId1, email).Token;
        string token2 = service.GenerateToken(userId2, email).Token;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken1 = handler.ReadJwtToken(token1);
        var jwtToken2 = handler.ReadJwtToken(token2);

        var sub1 = jwtToken1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var sub2 = jwtToken2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

        sub1.Should().Be(userId1.ToString());
        sub2.Should().Be(userId2.ToString());
        sub1.Should().NotBe(sub2);
    }
}