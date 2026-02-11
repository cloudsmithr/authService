using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.ResetPassword;
using AuthService.Application.Services.ResetPassword;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;

namespace AuthService.UnitTests.AuthService.Application.Services;

public class ResetPasswordServiceTests
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IVerificationTokenService> _verificationTokenServiceMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly Mock<ILogger<ResetPasswordService>> _loggerMock;
    private readonly ResetPasswordServiceSettings _settings;

    private const string TokenContext = "PasswordReset";

    public ResetPasswordServiceTests()
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
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _loggerMock = new Mock<ILogger<ResetPasswordService>>();

        _settings = new ResetPasswordServiceSettings
        {
            HoursToLive = 24,
        };
    }

    [Fact]
    public async Task CreateAndSendResetPasswordEmail_Should_Create_Token_And_Send_Email()
    {
        // Arrange
        string rawToken = "raw-reset-token";
        string hashedToken = "hashed-reset-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "old-hash",
            PasswordSalt = "old-salt",
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock.Setup(x => x.GenerateVerificationToken()).Returns(rawToken);
        _verificationTokenServiceMock.Setup(x => x.HashToken(rawToken, TokenContext)).Returns(hashedToken);

        _emailServiceMock.Setup(x => x.SendPasswordResetEmailAsync(user.Email, rawToken))
            .Returns(Task.CompletedTask);

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        await service.CreateAndSendResetPasswordEmail(user, CancellationToken.None);

        // Assert
        var verification = await _db.PasswordResetVerifications.FirstOrDefaultAsync();
        verification.Should().NotBeNull();
        verification.UserId.Should().Be(user.Id);
        verification.HashedToken.Should().Be(hashedToken);
        verification.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));

        _emailServiceMock.Verify(x => x.SendPasswordResetEmailAsync(user.Email, rawToken), Times.Once);
    }

    [Fact]
    public async Task CreateAndSendResetPasswordEmail_Should_Remove_Previous_Tokens()
    {
        // Arrange
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash1",
            PasswordSalt = "salt1",
        };

        _db.Users.Add(user);
        _db.PasswordResetVerifications.Add(new PasswordResetVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = "old-token",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock.Setup(x => x.GenerateVerificationToken()).Returns("new-token");
        _verificationTokenServiceMock.Setup(x => x.HashToken("new-token", TokenContext)).Returns("new-hash");

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        await service.CreateAndSendResetPasswordEmail(user, CancellationToken.None);

        // Assert
        var tokens = await _db.PasswordResetVerifications.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().HaveCount(1);
        tokens[0].HashedToken.Should().Be("new-hash");
    }

    [Fact]
    public async Task ResetPassword_Should_Return_LinkNotFound_When_Token_Does_Not_Exist()
    {
        // Arrange
        string rawToken = "missing-token";
        string hashedToken = "hashed-missing";

        _verificationTokenServiceMock.Setup(x => x.HashToken(rawToken, TokenContext)).Returns(hashedToken);

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var result = await service.ResetPassword(rawToken, "new-pass", CancellationToken.None);

        // Assert
        result.Should().Be(ResetPasswordOutcome.LinkNotFound);
    }

    [Fact]
    public async Task ResetPassword_Should_Return_LinkExpired_When_Token_Expired()
    {
        // Arrange
        string rawToken = "expired-token";
        string hashedToken = "hashed-expired-token";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "expired@example.com",
            PasswordHash = "hash1",
            PasswordSalt = "salt1",
        };
        _db.Users.Add(user);

        _db.PasswordResetVerifications.Add(new PasswordResetVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock.Setup(x => x.HashToken(rawToken, TokenContext)).Returns(hashedToken);

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var result = await service.ResetPassword(rawToken, "new-pass", CancellationToken.None);

        // Assert
        result.Should().Be(ResetPasswordOutcome.LinkExpired);
    }

    [Fact]
    public async Task ResetPassword_Should_Update_User_When_Token_Valid()
    {
        // Arrange
        string rawToken = "valid-token";
        string hashedToken = "hashed-valid-token";
        string newHash = "new-hash";
        string newSalt = "new-salt";

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "old-hash",
            PasswordSalt = "old-salt"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.PasswordResetVerifications.Add(new PasswordResetVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock.Setup(x => x.HashToken(rawToken, TokenContext)).Returns(hashedToken);
        _passwordServiceMock.Setup(x => x.HashPassword("new-pass"))
            .ReturnsAsync((newHash, newSalt));

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var outcome = await service.ResetPassword(rawToken, "new-pass", CancellationToken.None);

        // Assert
        outcome.Should().Be(ResetPasswordOutcome.Success);

        var updatedUser = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        updatedUser.PasswordHash.Should().Be(newHash);
        updatedUser.PasswordSalt.Should().Be(newSalt);
    }

    [Fact]
    public async Task ResetPassword_Should_Return_LinkInvalid_When_User_Not_Updated()
    {
        // Arrange
        string rawToken = "valid-token";
        string hashedToken = "hashed-valid-token";

        // we have to turn off foreign keys for this test, which is purely defensive coding on the completely unlikely chance
        // that we'll no longer have foreign keys tying tables together
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await _db.SaveChangesAsync();

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.PasswordResetVerifications.Add(new PasswordResetVerification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HashedToken = hashedToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        _verificationTokenServiceMock.Setup(x => x.HashToken(rawToken, TokenContext)).Returns(hashedToken);
        _passwordServiceMock.Setup(x => x.HashPassword("new-pass")).ReturnsAsync(("hash", "salt"));

        ResetPasswordService service = new(
            _emailServiceMock.Object,
            _verificationTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _passwordServiceMock.Object,
            Options.Create(_settings),
            _db,
            _loggerMock.Object);

        // Act
        var result = await service.ResetPassword(rawToken, "new-pass", CancellationToken.None);

        // Assert
        result.Should().Be(ResetPasswordOutcome.LinkInvalid);
    }
}
