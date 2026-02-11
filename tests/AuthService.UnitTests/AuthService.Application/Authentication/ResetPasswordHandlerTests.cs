using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.ResetPassword;
using AuthService.Application.Interfaces;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class ResetPasswordHandlerTests
{
    private readonly Mock<IResetPasswordService> _resetPasswordServiceMock;
    private readonly Mock<ILogger<ResetPasswordHandler>> _loggerMock;
    private readonly ResetPasswordSettings _settings;

    public ResetPasswordHandlerTests()
    {
        _resetPasswordServiceMock = new Mock<IResetPasswordService>();
        _loggerMock = new Mock<ILogger<ResetPasswordHandler>>();
        
        _settings = new ResetPasswordSettings
        {
            MinimumDurationMs = 0 // Set to 0 for faster tests
        };
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_Password_Reset_Succeeds()
    {
        // Arrange
        string token = "valid-token-123";
        string newPassword = "NewSecurePassword123!";

        _resetPasswordServiceMock
            .Setup(x => x.ResetPassword(token, newPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResetPasswordOutcome.Success);

        ResetPasswordRequest request = new()
        {
            VerificationToken = token,
            Password = newPassword
        };
        
        ResetPasswordHandler handler = new(
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResetPasswordResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ResetPasswordOutcome.Success);
        result.Message.Should().BeNull();
        
        _resetPasswordServiceMock.Verify(
            x => x.ResetPassword(token, newPassword, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_BadRequest_For_Invalid_Requests(
        ResetPasswordRequest request,
        string reason)
    {
        // Arrange
        ResetPasswordHandler handler = new(
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act
        ResetPasswordResult result = await handler.Handle(request);

        // Assert
        result.Outcome.Should().Be(ResetPasswordOutcome.BadRequest, because: reason);
        result.Message.Should().NotBeNullOrEmpty();
        
        _resetPasswordServiceMock.Verify(
            x => x.ResetPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), 
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
            new ResetPasswordRequest { VerificationToken = "", Password = "ValidPassword123!" },
            "Token is empty"
        },
        new object[]
        {
            new ResetPasswordRequest { VerificationToken = "   ", Password = "ValidPassword123!" },
            "Token is whitespace"
        },
        new object[]
        {
            new ResetPasswordRequest { VerificationToken = null!, Password = "ValidPassword123!" },
            "Token is null"
        },
        new object[]
        {
            new ResetPasswordRequest { VerificationToken = "valid-token", Password = "" },
            "Password is empty"
        },
        new object[]
        {
            new ResetPasswordRequest { VerificationToken = "valid-token", Password = "   " },
            "Password is whitespace"
        },
        new object[]
        {
            new ResetPasswordRequest { VerificationToken = "valid-token", Password = null! },
            "Password is null"
        }
    };

    [Fact]
    public async Task Handle_Should_Throw_When_ResetPasswordService_Throws_OperationCanceledException()
    {
        // Arrange
        string token = "valid-token";
        string newPassword = "NewSecurePassword123!";

        _resetPasswordServiceMock
            .Setup(x => x.ResetPassword(token, newPassword, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        ResetPasswordRequest request = new()
        {
            VerificationToken = token,
            Password = newPassword
        };
        
        ResetPasswordHandler handler = new(
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
        string token = "valid-token";
        string newPassword = "NewSecurePassword123!";

        _resetPasswordServiceMock
            .Setup(x => x.ResetPassword(token, newPassword, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Password reset service failed"));

        ResetPasswordRequest request = new()
        {
            VerificationToken = token,
            Password = newPassword
        };
        
        ResetPasswordHandler handler = new(
            _resetPasswordServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            handler.Handle(request, CancellationToken.None));
    }
}