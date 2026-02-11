using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.RefreshToken;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class RefreshTokenHandlerTests
{
    private readonly AppDbContext _db;
    private readonly RefreshTokenHandler _handler;
    public RefreshTokenHandlerTests()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        DbContextOptions<AppDbContext> dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        _db = new AppDbContext(dbOptions);
        _db.Database.EnsureCreated(); 
        
        JwtService jwtService = CreateJwtService();
        Mock<ILogger<RefreshTokenHandler>> logger = new ();
        Mock<ILogger<RefreshTokenService>> refreshTokenLogger = new ();
        
        RefreshTokenSettings refreshTokenSettings = new()
        {
            RefreshTokenLength = 64,
            RefreshTokenLifeTimeInHours = 24,
            RefreshTokenPurgeCutoffInDays = 30
        };
        
        RefreshTokenService refreshTokenService = new RefreshTokenService(_db, Options.Create(refreshTokenSettings), refreshTokenLogger.Object);
        
        _handler = new RefreshTokenHandler(_db, jwtService, logger.Object, refreshTokenService);
    }
    
    private JwtService CreateJwtService()
    {
        JwtSettings jwtSettings = new()
        {
            Issuer = "testIssuer",
            Audience = "testAudience",
            Key = "this_is_a_very_long_test_key_at_least_32_bytes_long!!!!",
            ExpiresInMinutes = 15,
        };
        return new JwtService(Options.Create(jwtSettings));
    }
    
    [Fact]
    public async Task Handle_EmptyRefreshToken_ReturnsInvalidInput()
    {
        // Arrange
        RefreshTokenRequest request = new RefreshTokenRequest { RefreshToken = "" };
        Guid userId = Guid.NewGuid();

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.BadRequest);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        result.Message.Should().Be("Refresh token is required.");
    }

    [Fact]
    public async Task Handle_NullRefreshToken_ReturnsInvalidInput()
    {
        // Arrange
        RefreshTokenRequest request = new () { RefreshToken = null! };
        Guid userId = Guid.NewGuid();

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.BadRequest);
        result.Message.Should().Be("Refresh token is required.");
    }

    [Fact]
    public async Task Handle_EmptyUserId_ReturnsInvalidInput()
    {
        // Arrange
        RefreshTokenRequest request = new () { RefreshToken = "valid-token" };
        Guid userId = Guid.Empty;

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.BadRequest);
        result.Message.Should().Be("UserId is required.");
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsTokenNotFound()
    {
        // Arrange
        RefreshTokenRequest request = new () { RefreshToken = "non-existent-token" };
        Guid userId = Guid.NewGuid();

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.TokenNotFound);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsTokenExpired()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        
        User user = new ()
        {
            Id = userId,
            Email = "testEmail@ptest.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();  
        
        const string tokenValue = "expired-token";
        RefreshToken expiredToken = new ()
        {
            Id = Guid.NewGuid(),
            Token = tokenValue,
            UserId = userId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            RevokedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        await _db.RefreshTokens.AddAsync(expiredToken);
        await _db.SaveChangesAsync();

        RefreshTokenRequest request = new () { RefreshToken = tokenValue };

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.TokenExpired);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RevokedToken_ReturnsTokenRevoked()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        
        User user = new ()
        {
            Id = userId,
            Email = "testEmail@ptest.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();  
        
        const string tokenValue = "revoked-token";
        RefreshToken revokedToken = new ()
        {
            Id = Guid.NewGuid(),
            Token = tokenValue,
            UserId = userId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1), // Still valid expiry
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-30), // Revoked 30 mins ago
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        await _db.RefreshTokens.AddAsync(revokedToken);
        await _db.SaveChangesAsync();

        RefreshTokenRequest request = new () { RefreshToken = tokenValue };

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.TokenRevoked);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
    }
    
    [Fact]
    public async Task Handle_ValidTokenNotExpiringSoon_ReturnsSuccessWithSameRefreshToken()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string email = "testUser@test.com";
        const string tokenValue = "valid-token";

        User user = new ()
        {
            Id = userId,
            Email = email,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        RefreshToken validToken = new ()
        {
            Id = Guid.NewGuid(),
            Token = tokenValue,
            UserId = userId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(2), // Not expiring soon
            RevokedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        await _db.RefreshTokens.AddAsync(validToken);
        await _db.SaveChangesAsync();

        RefreshTokenRequest request = new () { RefreshToken = tokenValue };

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.Success);
        result.AccessToken.Token.Should().NotBeNull().And.NotBe(tokenValue);
        result.RefreshToken.Token.Should().Be(tokenValue); // Same refresh token returned
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidTokenExpiringSoon_ReturnsSuccessWithNewRefreshToken()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string email = "testUser@test.com";
        const string tokenValue = "expiring-soon-token";

        User user = new ()
        {
            Id = userId,
            Email = email,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        RefreshToken expiringSoonToken = new ()
        {
            Id = Guid.NewGuid(),
            Token = tokenValue,
            UserId = userId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(12), // Expires in less than 1 day
            RevokedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
        await _db.RefreshTokens.AddAsync(expiringSoonToken);
        await _db.SaveChangesAsync();

        RefreshTokenRequest request = new () { RefreshToken = tokenValue };

        // Act
        RefreshTokenResult result = await _handler.Handle(request, userId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RefreshTokenOutcome.Success);
        result.AccessToken.Should().NotBeNull().And.NotBe(tokenValue);
        result.RefreshToken.Should().NotBeNull().And.NotBe(tokenValue);
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        RefreshTokenRequest request = new () { RefreshToken = "test-token" };
        Guid userId = Guid.NewGuid();
        CancellationToken cancellationToken = new (true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _handler.Handle(request, userId, cancellationToken));
    }
}