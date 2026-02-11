using FluentAssertions;
using Microsoft.Extensions.Options;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Infrastructure.Services;

public class VerificationTokenServiceTests
{
    private readonly VerificationTokenSettings _settings;

    public VerificationTokenServiceTests()
    {
        _settings = new VerificationTokenSettings
        {
            TokenSize = 32,
            SecretKey = "SuperDeeDuperDeeSecretKeyFlabadooey"
        };
    }

    [Fact]
    public void GenerateVerificationToken_Should_Return_NonEmpty_String()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string token = service.GenerateVerificationToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateVerificationToken_Should_Return_Base64_String()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string token = service.GenerateVerificationToken();

        // Assert
        // Base64 strings should be convertible back to bytes without exception
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateVerificationToken_Should_Generate_Unique_Tokens()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string token1 = service.GenerateVerificationToken();
        string token2 = service.GenerateVerificationToken();
        string token3 = service.GenerateVerificationToken();

        // Assert
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);
    }

    [Fact]
    public void GenerateVerificationToken_Should_Respect_TokenSize_Setting()
    {
        // Arrange
        var customSettings = new VerificationTokenSettings { TokenSize = 64, SecretKey = "SuperDeeDuperDeeSecretKeyFlabadooey" };
        VerificationTokenService service = new(Options.Create(customSettings));

        // Act
        string token = service.GenerateVerificationToken();
        byte[] tokenBytes = Convert.FromBase64String(token);

        // Assert
        tokenBytes.Length.Should().Be(64);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void GenerateVerificationToken_Should_Work_With_Different_Sizes(int tokenSize)
    {
        // Arrange
        var customSettings = new VerificationTokenSettings { TokenSize = tokenSize, SecretKey = "SuperDeeDuperDeeSecretKeyFlabadooey"};
        VerificationTokenService service = new(Options.Create(customSettings));

        // Act
        string token = service.GenerateVerificationToken();
        byte[] tokenBytes = Convert.FromBase64String(token);

        // Assert
        tokenBytes.Length.Should().Be(tokenSize);
    }

    [Fact]
    public void HashToken_Should_Return_NonEmpty_String()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));
        string token = "test-token";

        // Act
        string hash = service.HashToken(token,"TEST");

        // Assert
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashToken_Should_Return_Consistent_Hash_For_Same_Input()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));
        string token = "test-token";

        // Act
        string hash1 = service.HashToken(token,"TEST");
        string hash2 = service.HashToken(token,"TEST");
        string hash3 = service.HashToken(token,"TEST");

        // Assert
        hash1.Should().Be(hash2);
        hash2.Should().Be(hash3);
    }

    [Fact]
    public void HashToken_Should_Return_Different_Hash_For_Different_Input()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string hash1 = service.HashToken("token1","TEST");
        string hash2 = service.HashToken("token2","TEST");
        string hash3 = service.HashToken("token3","TEST");

        // Assert
        hash1.Should().NotBe(hash2);
        hash2.Should().NotBe(hash3);
        hash1.Should().NotBe(hash3);
    }

    [Fact]
    public void HashToken_Should_Return_Base64_String()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));
        string token = "test-token";

        // Act
        string hash = service.HashToken(token, "TEST");

        // Assert
        var act = () => Convert.FromBase64String(hash);
        act.Should().NotThrow();
    }

    [Fact]
    public void HashToken_Should_Produce_SHA256_Length_Hash()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));
        string token = "test-token";

        // Act
        string hash = service.HashToken(token, "TEST");
        byte[] hashBytes = Convert.FromBase64String(hash);

        // Assert
        // SHA256 produces 32 bytes
        hashBytes.Length.Should().Be(32);
    }

    [Fact]
    public void HashToken_Should_Be_Case_Sensitive()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string hash1 = service.HashToken("Token", "TEST");
        string hash2 = service.HashToken("token","TEST");
        string hash3 = service.HashToken("TOKEN","TEST");

        // Assert
        hash1.Should().NotBe(hash2);
        hash2.Should().NotBe(hash3);
        hash1.Should().NotBe(hash3);
    }

    [Fact]
    public void GenerateAndHash_Should_Create_Valid_Token_And_Hash()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string token = service.GenerateVerificationToken();
        string hash = service.HashToken(token,"TEST");

        // Assert
        token.Should().NotBeNullOrEmpty();
        hash.Should().NotBeNullOrEmpty();
        token.Should().NotBe(hash); // Original token and hash should be different
    }

    [Fact]
    public void HashToken_Should_Handle_Empty_String()
    {
        // Arrange
        VerificationTokenService service = new(Options.Create(_settings));

        // Act
        string hash = service.HashToken("","TEST");

        // Assert
        hash.Should().NotBeNullOrEmpty();
        // Even empty strings produce a valid SHA256 hash
    }
}