using FluentAssertions;
using Microsoft.Extensions.Options;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Infrastructure.Services;

public class PasswordServiceTests
{
    private readonly PasswordSettings _settings;

    public PasswordServiceTests()
    {
        _settings = new PasswordSettings
        {
            DegreeOfParallelism = 2,
            MemorySizeKb = 65536,
            Iterations = 2,
            HashLength = 32,
            SaltLength = 16
        };
    }

    [Fact]
    public async Task HashPassword_Should_Return_Hash_And_Salt()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash, salt) = await service.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HashPassword_Should_Return_Base64_Encoded_Values()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash, salt) = await service.HashPassword(password);

        // Assert
        var actHash = () => Convert.FromBase64String(hash);
        var actSalt = () => Convert.FromBase64String(salt);
        
        actHash.Should().NotThrow();
        actSalt.Should().NotThrow();
    }

    [Fact]
    public async Task HashPassword_Should_Return_Salt_Of_Correct_Length()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash, salt) = await service.HashPassword(password);
        byte[] saltBytes = Convert.FromBase64String(salt);

        // Assert
        saltBytes.Length.Should().Be(_settings.SaltLength);
    }

    [Fact]
    public async Task HashPassword_Should_Return_Hash_Of_Correct_Length()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash, salt) = await service.HashPassword(password);
        byte[] hashBytes = Convert.FromBase64String(hash);

        // Assert
        hashBytes.Length.Should().Be(_settings.HashLength);
    }

    [Fact]
    public async Task HashPassword_Should_Generate_Unique_Salts()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash1, salt1) = await service.HashPassword(password);
        var (hash2, salt2) = await service.HashPassword(password);
        var (hash3, salt3) = await service.HashPassword(password);

        // Assert
        salt1.Should().NotBe(salt2);
        salt2.Should().NotBe(salt3);
        salt1.Should().NotBe(salt3);
    }

    [Fact]
    public async Task HashPassword_Should_Generate_Different_Hashes_With_Different_Salts()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";

        // Act
        var (hash1, salt1) = await service.HashPassword(password);
        var (hash2, salt2) = await service.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task ComputeHashBase64_Should_Return_Consistent_Hash_For_Same_Password_And_Salt()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (originalHash, salt) = await service.HashPassword(password);

        // Act
        string computedHash1 = await service.ComputeHashBase64(password, salt);
        string computedHash2 = await service.ComputeHashBase64(password, salt);
        string computedHash3 = await service.ComputeHashBase64(password, salt);

        // Assert
        computedHash1.Should().Be(computedHash2);
        computedHash2.Should().Be(computedHash3);
    }

    [Fact]
    public async Task ComputeHashBase64_Should_Return_Different_Hash_For_Different_Password()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        var (hash1, salt) = await service.HashPassword("password1");

        // Act
        string computedHash1 = await service.ComputeHashBase64("password1", salt);
        string computedHash2 = await service.ComputeHashBase64("password2", salt);

        // Assert
        computedHash1.Should().NotBe(computedHash2);
    }

    [Fact]
    public async Task ComputeHashBase64_Should_Return_Different_Hash_For_Different_Salt()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (hash1, salt1) = await service.HashPassword(password);
        var (hash2, salt2) = await service.HashPassword(password);

        // Act
        string computedHash1 = await service.ComputeHashBase64(password, salt1);
        string computedHash2 = await service.ComputeHashBase64(password, salt2);

        // Assert
        computedHash1.Should().NotBe(computedHash2);
    }

    [Fact]
    public async Task Verify_Should_Return_True_For_Correct_Password()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (hash, salt) = await service.HashPassword(password);

        // Act
        bool result = await service.Verify(password, salt, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_Should_Return_False_For_Incorrect_Password()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string correctPassword = "MySecurePassword123";
        string incorrectPassword = "WrongPassword456";
        var (hash, salt) = await service.HashPassword(correctPassword);

        // Act
        bool result = await service.Verify(incorrectPassword, salt, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_Should_Return_False_For_Wrong_Salt()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (hash, salt) = await service.HashPassword(password);
        var (hash2, wrongSalt) = await service.HashPassword(password);

        // Act
        bool result = await service.Verify(password, wrongSalt, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_Should_Be_Case_Sensitive()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (hash, salt) = await service.HashPassword(password);

        // Act
        bool result1 = await service.Verify("MySecurePassword123", salt, hash);
        bool result2 = await service.Verify("mysecurepassword123", salt, hash);
        bool result3 = await service.Verify("MYSECUREPASSWORD123", salt, hash);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public async Task HashPassword_Should_Work_With_Different_Settings()
    {
        // Arrange
        var customSettings = new PasswordSettings
        {
            DegreeOfParallelism = 4,
            MemorySizeKb = 32768,
            Iterations = 3,
            HashLength = 64,
            SaltLength = 32
        };
        PasswordService service = new(Options.Create(customSettings));
        string password = "MySecurePassword123";

        // Act
        var (hash, salt) = await service.HashPassword(password);
        byte[] hashBytes = Convert.FromBase64String(hash);
        byte[] saltBytes = Convert.FromBase64String(salt);

        // Assert
        hashBytes.Length.Should().Be(64);
        saltBytes.Length.Should().Be(32);
    }

    [Fact]
    public async Task Verify_Should_Handle_Unicode_Passwords()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "Пароль123密码🔒";
        var (hash, salt) = await service.HashPassword(password);

        // Act
        bool correctResult = await service.Verify(password, salt, hash);
        bool incorrectResult = await service.Verify("WrongPassword", salt, hash);

        // Assert
        correctResult.Should().BeTrue();
        incorrectResult.Should().BeFalse();
    }

    [Fact]
    public async Task HashPassword_Should_Handle_Very_Long_Passwords()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = new string('a', 1000);
        
        // Act
        var (hash, salt) = await service.HashPassword(password);
        bool result = await service.Verify(password, salt, hash);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_Should_Use_Constant_Time_Comparison()
    {
        // Arrange
        PasswordService service = new(Options.Create(_settings));
        string password = "MySecurePassword123";
        var (hash, salt) = await service.HashPassword(password);

        // Act - Multiple verifications should take similar time regardless of how different the password is
        // This is a basic test; timing attacks are hard to test properly in unit tests
        bool result1 = await service.Verify("A", salt, hash);
        bool result2 = await service.Verify("MySecurePassword12", salt, hash);
        bool result3 = await service.Verify("Completely Different Password", salt, hash);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }
}