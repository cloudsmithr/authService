using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.VerifyEmail;
using AuthService.Application.Services.EmailVerification;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;

namespace AuthService.UnitTests.AuthService.Application.Services;

public class EmailVerificationServiceTests
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IVerificationTokenService> _verificationTokenServiceMock;
    private readonly Mock<ILogger<EmailVerificationService>> _loggerMock;
    private readonly EmailVerificationSettings _settings;

    private const string TokenContext = "EmailVerification";

    public EmailVerificationServiceTests()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        DbContextOptions<AppDbContext> dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        _db = new AppDbContext(dbOptions);
        _db.Database.EnsureCreated(); 
        
        _emailServiceMock = new Mock<IEmailService>();
        _verificationTokenServiceMock = new Mock<IVerificationTokenService>();
        _loggerMock = new Mock<ILogger<EmailVerificationService>>();

        _settings = new EmailVerificationSettings
        {
            HoursToLive = 24
        };
    }

    [Fact]
    public async Task CreateAndSendEmailVerification_Should_Create_Token_And_Send_Email()
    {
        // Arrange
        string rawToken = "raw-verification-token";
        string hashedToken = "hashed-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        _verificationTokenServiceMock
            .Setup(x => x.GenerateVerificationToken())
            .Returns(rawToken);

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken, TokenContext))
            .Returns(hashedToken);

        _emailServiceMock
            .Setup(x => x.SendVerificationEmailAsync(user.Email, rawToken))
            .Returns(Task.CompletedTask);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        await service.CreateAndSendEmailVerification(user, CancellationToken.None);

        // Assert
        var verification = await _db.EmailVerifications.FirstOrDefaultAsync();
        verification.Should().NotBeNull();
        verification!.UserId.Should().Be(user.Id);
        verification.HashedToken.Should().Be(hashedToken);
        verification.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));

        _verificationTokenServiceMock.Verify(x => x.GenerateVerificationToken(), Times.Once);
        _verificationTokenServiceMock.Verify(x => x.HashToken(rawToken,TokenContext), Times.Once);
        _emailServiceMock.Verify(x => x.SendVerificationEmailAsync(user.Email, rawToken), Times.Once);
    }

    [Fact]
    public async Task CreateAndSendEmailVerification_Should_Delete_Old_Tokens()
    {
        // Arrange
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        // Add old verification token
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = "old-token",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock
            .Setup(x => x.GenerateVerificationToken())
            .Returns("new-token");

        _verificationTokenServiceMock
            .Setup(x => x.HashToken("new-token",TokenContext))
            .Returns("new-hashed-token");

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        await service.CreateAndSendEmailVerification(user, CancellationToken.None);

        // Assert
        var verifications = await _db.EmailVerifications.Where(v => v.UserId == user.Id).ToListAsync();
        verifications.Should().HaveCount(1);
        verifications[0].HashedToken.Should().Be("new-hashed-token");
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_Success_And_User_When_Token_Valid()
    {
        // Arrange
        string rawToken = "valid-token";
        string hashedToken = "hashed-valid-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken,TokenContext))
            .Returns(hashedToken);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var (outcome, resultUser) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Assert
        outcome.Should().Be(VerifyEmailOutcome.Success);
        resultUser.Should().NotBeNull();
        resultUser!.Id.Should().Be(user.Id);
        resultUser.Email.Should().Be(user.Email);

        // Verify user was marked as verified
        // use as no tracking to make sure we're actually re-querying and don't get a stale reference
        var updatedUser = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        updatedUser!.EmailVerified.Should().BeTrue();

        // Verify token was deleted
        var verification = await _db.EmailVerifications.FirstOrDefaultAsync(v => v.HashedToken == hashedToken);
        verification.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_LinkNotFound_When_Token_Does_Not_Exist()
    {
        // Arrange
        string rawToken = "nonexistent-token";
        string hashedToken = "hashed-nonexistent-token";

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken,TokenContext))
            .Returns(hashedToken);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var (outcome, user) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Assert
        outcome.Should().Be(VerifyEmailOutcome.LinkNotFound);
        user.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_LinkExpired_When_Token_Expired()
    {
        // Arrange
        string rawToken = "expired-token";
        string hashedToken = "hashed-expired-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };

        _db.Users.Add(user);
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1) // Expired
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken,TokenContext))
            .Returns(hashedToken);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var (outcome, resultUser) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Assert
        outcome.Should().Be(VerifyEmailOutcome.LinkExpired);
        resultUser.Should().BeNull();

        // User should still not be verified
        var updatedUser = await _db.Users.FindAsync(user.Id);
        updatedUser!.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmail_Should_Return_LinkInvalid_When_User_Already_Verified()
    {
        // Arrange
        string rawToken = "valid-token";
        string hashedToken = "hashed-valid-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true // Already verified
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken,TokenContext))
            .Returns(hashedToken);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var (outcome, resultUser) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Assert
        outcome.Should().Be(VerifyEmailOutcome.LinkInvalid);
        resultUser.Should().BeNull();

        // Token should still be cleaned up
        var verification = await _db.EmailVerifications.FirstOrDefaultAsync(v => v.HashedToken == hashedToken);
        verification.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_Should_Handle_Race_Condition_When_Two_Requests_Verify_Same_Token()
    {
        // Arrange
        string rawToken = "valid-token";
        string hashedToken = "hashed-valid-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };

        _db.Users.Add(user);
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock
            .Setup(x => x.HashToken(rawToken,TokenContext))
            .Returns(hashedToken);

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act - First verification
        var (outcome1, user1) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Second verification attempt (simulating race condition/reuse)
        // Need to re-add the token since first call deleted it
        _db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        var (outcome2, user2) = await service.VerifyEmail(rawToken, CancellationToken.None);

        // Assert
        outcome1.Should().Be(VerifyEmailOutcome.Success);
        user1.Should().NotBeNull();

        outcome2.Should().Be(VerifyEmailOutcome.LinkInvalid);
        user2.Should().BeNull();
    }

    [Fact]
    public async Task CreateAndSendEmailVerification_Should_Use_Correct_Expiration_Time()
    {
        // Arrange
        var customSettings = new EmailVerificationSettings
        {
            HoursToLive = 48 // Custom expiration
        };

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        };
        
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        _verificationTokenServiceMock
            .Setup(x => x.GenerateVerificationToken())
            .Returns("token");

        _verificationTokenServiceMock
            .Setup(x => x.HashToken("token",TokenContext))
            .Returns("hashed");

        EmailVerificationService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            Options.Create(customSettings),
            _db,
            _loggerMock.Object);

        // Act
        await service.CreateAndSendEmailVerification(user, CancellationToken.None);

        // Assert
        var verification = await _db.EmailVerifications.FirstOrDefaultAsync();
        verification.Should().NotBeNull();
        verification!.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromSeconds(5));
    }
}