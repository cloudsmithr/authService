using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.SendResetPasswordEmail;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class SendResetPasswordEmailHandlerTests
{
    private readonly AppDbContext _db;
    private readonly Mock<IResetPasswordService> _resetPasswordServiceMock;
    private readonly Mock<ILogger<SendResetPasswordEmailHandler>> _loggerMock;
    private readonly SendResetPasswordEmailSettings _settings;

    public SendResetPasswordEmailHandlerTests()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new AppDbContext(options);
        _resetPasswordServiceMock = new Mock<IResetPasswordService>();
        _loggerMock = new Mock<ILogger<SendResetPasswordEmailHandler>>();
        
        _settings = new SendResetPasswordEmailSettings
        {
            MinimumDurationMs = 0 // Set to 0 for faster tests
        };
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Email_Sent_Successfully()
    {
        // Arrange
        string email = "test@example.com";
        
        User existingUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        };

        await _db.Users.AddAsync(existingUser);
        await _db.SaveChangesAsync();

        _resetPasswordServiceMock
            .Setup(x => x.CreateAndSendResetPasswordEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SendResetPasswordEmailRequest request = new() { Email = email };
        
        SendResetPasswordEmailHandler handler = new(
            _db,
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        SendResetPasswordEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(SendResetPasswordEmailOutcome.Success);
        result.Message.Should().BeNull();
        
        _resetPasswordServiceMock.Verify(
            x => x.CreateAndSendResetPasswordEmail(
                It.Is<User>(u => u.Email == email), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_EmailNotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        string email = "nonexistent@example.com";

        SendResetPasswordEmailRequest request = new() { Email = email };
        
        SendResetPasswordEmailHandler handler = new(
            _db,
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        SendResetPasswordEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(SendResetPasswordEmailOutcome.EmailNotFound);
        result.Message.Should().BeNull();
        
        _resetPasswordServiceMock.Verify(
            x => x.CreateAndSendResetPasswordEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_BadRequest_For_Invalid_Requests(
        SendResetPasswordEmailRequest request,
        string reason)
    {
        // Arrange
        SendResetPasswordEmailHandler handler = new(
            _db,
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        SendResetPasswordEmailResult result = await handler.Handle(request);

        // Assert
        result.Outcome.Should().Be(SendResetPasswordEmailOutcome.BadRequest, because: reason);
        result.Message.Should().NotBeNullOrEmpty();
        
        _resetPasswordServiceMock.Verify(
            x => x.CreateAndSendResetPasswordEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    public static IEnumerable<object[]> InvalidRequests => new List<object[]>
    {
        new object[]
        {
            null!,
            "Request is null"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = "" },
            "Email is empty"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = "   " },
            "Email is whitespace"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = null! },
            "Email is null"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = "invalid-email" },
            "Email is invalid format"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = "@example.com" },
            "Email is missing local part"
        },
        new object[]
        {
            new SendResetPasswordEmailRequest { Email = "test@" },
            "Email is missing domain"
        }
    };

    [Fact]
    public async Task Handle_Should_Throw_When_ResetPasswordService_Throws_OperationCanceledException()
    {
        // Arrange
        string email = "test@example.com";
        
        User existingUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        };

        await _db.Users.AddAsync(existingUser);
        await _db.SaveChangesAsync();

        _resetPasswordServiceMock
            .Setup(x => x.CreateAndSendResetPasswordEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        SendResetPasswordEmailRequest request = new() { Email = email };
        
        SendResetPasswordEmailHandler handler = new(
            _db,
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_When_ResetPasswordService_Throws_Exception()
    {
        // Arrange
        string email = "test@example.com";
        
        User existingUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        };

        await _db.Users.AddAsync(existingUser);
        await _db.SaveChangesAsync();

        _resetPasswordServiceMock
            .Setup(x => x.CreateAndSendResetPasswordEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email service failed"));

        SendResetPasswordEmailRequest request = new() { Email = email };
        
        SendResetPasswordEmailHandler handler = new(
            _db,
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            handler.Handle(request, CancellationToken.None));
    }
}