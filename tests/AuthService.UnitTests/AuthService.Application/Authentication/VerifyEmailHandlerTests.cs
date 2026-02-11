using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.VerifyEmail;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class VerifyEmailHandlerTests
{
    private readonly Mock<IEmailVerificationService> _emailVerificationServiceMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;
    private readonly Mock<ILogger<VerifyEmailHandler>> _loggerMock;
    private readonly VerifyEmailSettings _settings;

    public VerifyEmailHandlerTests()
    {
        _emailVerificationServiceMock = new Mock<IEmailVerificationService>();
        _jwtServiceMock = new Mock<IJwtService>();
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _loggerMock = new Mock<ILogger<VerifyEmailHandler>>();
        
        _settings = new VerifyEmailSettings
        {
            MinimumDurationMs = 0 // Set to 0 for faster tests
        };
    }

    [Fact]
    public async Task Handle_Should_Return_Success_With_Tokens_When_Verification_Succeeds()
    {
        // Arrange
        string token = "valid-token-123";
        ApiToken accessToken = new ApiToken("access-token", DateTime.UtcNow.AddMinutes(5));
        ApiToken refreshToken = new ApiToken("refresh-token", DateTime.UtcNow.AddMinutes(5));
        
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        };

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailOutcome.Success, user));

        _jwtServiceMock
            .Setup(x => x.GenerateToken(user.Id, user.Email))
            .Returns(accessToken);

        _refreshTokenServiceMock
            .Setup(x => x.GenerateRefreshToken(user, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        VerifyEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(VerifyEmailOutcome.Success);
        result.AccessToken.Token.Should().Be(accessToken.Token);
        result.RefreshToken.Token.Should().Be(refreshToken.Token);
        result.Message.Should().BeNull();
        
        _jwtServiceMock.Verify(x => x.GenerateToken(user.Id, user.Email), Times.Once);
        _refreshTokenServiceMock.Verify(
            x => x.GenerateRefreshToken(user, null, true, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_LinkNotFound_When_Token_Not_Found()
    {
        // Arrange
        string token = "invalid-token";

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailOutcome.LinkNotFound, (User?)null));

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        VerifyEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(VerifyEmailOutcome.LinkNotFound);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        
        _jwtServiceMock.Verify(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _refreshTokenServiceMock.Verify(
            x => x.GenerateRefreshToken(It.IsAny<User>(), It.IsAny<RefreshToken>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_LinkExpired_When_Token_Expired()
    {
        // Arrange
        string token = "expired-token";

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailOutcome.LinkExpired, (User?)null));

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        VerifyEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(VerifyEmailOutcome.LinkExpired);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        
        _jwtServiceMock.Verify(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_LinkInvalid_When_Link_Invalid()
    {
        // Arrange
        string token = "invalid-token";

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailOutcome.LinkInvalid, (User?)null));

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        VerifyEmailResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(VerifyEmailOutcome.LinkInvalid);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
    }
    
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_BadRequest_For_Invalid_Requests(
        VerifyEmailRequest request, 
        string reason)
    {
        // Arrange
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        VerifyEmailResult result = await handler.Handle(request);

        // Assert
        result.Outcome.Should().Be(VerifyEmailOutcome.BadRequest, because: reason);
        result.Message.Should().NotBeNullOrEmpty();
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        
        _emailVerificationServiceMock.Verify(
            x => x.VerifyEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
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
            new VerifyEmailRequest { VerificationToken = "" },
            "Token is empty"
        },
        new object[]
        {
            new VerifyEmailRequest { VerificationToken = "   " },
            "Token is whitespace"
        },
        new object[]
        {
            new VerifyEmailRequest { VerificationToken = null! },
            "Token is null"
        }
    };

    [Fact]
    public async Task Handle_Should_Throw_When_EmailVerificationService_Throws_OperationCanceledException()
    {
        // Arrange
        string token = "valid-token";

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
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
        string token = "valid-token";

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Verification service failed"));

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Purge_Old_Tokens_When_Generating_Refresh_Token()
    {
        // Arrange
        string token = "valid-token";
        
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAtUtc = DateTime.UtcNow,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            EmailVerified = true
        };

        _emailVerificationServiceMock
            .Setup(x => x.VerifyEmail(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailOutcome.Success, user));

        _jwtServiceMock
            .Setup(x => x.GenerateToken(user.Id, user.Email))
            .Returns(new ApiToken("access-token", DateTime.UtcNow.AddDays(1)));

        _refreshTokenServiceMock
            .Setup(x => x.GenerateRefreshToken(user, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiToken("refresh-token",  DateTime.UtcNow.AddDays(1)));

        VerifyEmailRequest request = new() { VerificationToken = token };
        
        VerifyEmailHandler handler = new(
            _emailVerificationServiceMock.Object,
            _jwtServiceMock.Object,
            _refreshTokenServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _refreshTokenServiceMock.Verify(
            x => x.GenerateRefreshToken(user, null, true, It.IsAny<CancellationToken>()), 
            Times.Once,
            "purgeOldTokens should be true");
    }
}