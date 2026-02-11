using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.ResendEmailVerification;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class ResendEmailVerificationHandlerTests
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<ResendEmailVerificationHandler>> _loggerMock;
    private readonly Mock<IEmailVerificationService> _emailVerificationServiceMock;
    private readonly ResendEmailVerificationSettings _settings;

    public ResendEmailVerificationHandlerTests()
    {
        DbContextOptions<AppDbContext> dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(dbOptions);
        _loggerMock = new Mock<ILogger<ResendEmailVerificationHandler>>();
        _emailVerificationServiceMock = new Mock<IEmailVerificationService>();

        _settings = new ResendEmailVerificationSettings
        {
            MinimumDurationMs = 0 // Set to 0 for faster tests
        };
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_User_Exists_And_Not_Verified()
    {
        // Arrange
        string email = "unverified@example.com";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        });
        await _db.SaveChangesAsync();

        ResendEmailVerificationRequest request = new() { Email = email };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResendEmailVerificationResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ResendEmailVerificationOutcome.Success);
        result.Message.Should().BeNull();

        _emailVerificationServiceMock.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_EmailNotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        ResendEmailVerificationRequest request = new() { Email = "nonexistent@example.com" };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResendEmailVerificationResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ResendEmailVerificationOutcome.EmailNotFound);
        result.Message.Should().BeNull();

        _emailVerificationServiceMock.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_EmailAlreadyVerified_When_User_Already_Verified()
    {
        // Arrange
        string email = "verified@example.com";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        });
        await _db.SaveChangesAsync();

        ResendEmailVerificationRequest request = new() { Email = email };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResendEmailVerificationResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ResendEmailVerificationOutcome.EmailAlreadyVerified);
        result.Message.Should().BeNull();

        _emailVerificationServiceMock.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Normalize_Email_To_Lowercase()
    {
        // Arrange
        string emailLower = "test@example.com";
        string emailMixed = "TeSt@ExAmPlE.cOm";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = emailLower,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        });
        await _db.SaveChangesAsync();

        ResendEmailVerificationRequest request = new() { Email = emailMixed };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResendEmailVerificationResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ResendEmailVerificationOutcome.Success);

        _emailVerificationServiceMock.Verify(
            x => x.CreateAndSendEmailVerification(
                It.Is<User>(u => u.Email == emailLower),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_BadRequest_For_Invalid_Requests(
        ResendEmailVerificationRequest request,
        string reason,
        string expectedMessage)
    {
        // Arrange
        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResendEmailVerificationResult result = await handler.Handle(request);

        // Assert
        result.Outcome.Should().Be(ResendEmailVerificationOutcome.BadRequest, because: reason);
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain(expectedMessage);

        _emailVerificationServiceMock.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public static IEnumerable<object[]> InvalidRequests => new List<object[]>
    {
        new object[]
        {
            null!,
            "Request is null",
            "request can't be null"
        },
        new object[]
        {
            new ResendEmailVerificationRequest { Email = "" },
            "Email is empty",
            "email can't be empty"
        },
        new object[]
        {
            new ResendEmailVerificationRequest { Email = "   " },
            "Email is whitespace",
            "email can't be empty"
        },
        new object[]
        {
            new ResendEmailVerificationRequest { Email = null! },
            "Email is null",
            "email can't be empty"
        }
    };

    [Fact]
    public async Task Handle_Should_Throw_When_EmailVerificationService_Throws_OperationCanceledException()
    {
        // Arrange
        string email = "test@example.com";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        });
        await _db.SaveChangesAsync();

        _emailVerificationServiceMock
            .Setup(x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        ResendEmailVerificationRequest request = new() { Email = email };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_When_EmailVerificationService_Throws_Exception()
    {
        // Arrange
        string email = "test@example.com";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = false
        });
        await _db.SaveChangesAsync();

        _emailVerificationServiceMock
            .Setup(x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email service failed"));

        ResendEmailVerificationRequest request = new() { Email = email };

        ResendEmailVerificationHandler handler = new(
            _db,
            _emailVerificationServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(request, CancellationToken.None));
    }
}