using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using AuthService.Application.Authentication.Logout;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class LogoutUserHandlerTests
{
    private static AppDbContext CreateDb()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }
    
    private static RefreshToken CreateRefreshToken(
        Guid userId,
        string token,
        DateTime? expiresAtUtc = null,
        DateTime? revokedAtUtc = null)
    {
        return new RefreshToken 
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddHours(1),
            RevokedAtUtc = revokedAtUtc
        };
    }
    
    [Fact]
    public async Task Handle_Should_Revoke_Valid_Token_And_Return_Revoked()
    {
        AppDbContext db = CreateDb();
        Guid userId = Guid.NewGuid();
        string token = "valid-token-123";

        db.RefreshTokens.Add(CreateRefreshToken(userId, token));
        await db.SaveChangesAsync();

        LogoutUserHandler handler = new LogoutUserHandler(db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new LogoutUserRequest { RefreshTokenToRevoke = token };

        LogoutUserResult result = await handler.Handle(request, userId);

        result.Outcome.Should().Be(LogoutUserOutcome.SuccessfullyRevoked);
        RefreshToken stored = await db.RefreshTokens.FirstAsync(rt => rt.Token == token);
        stored.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_Token_Missing()
    {
        AppDbContext db = CreateDb();
        Guid userId = Guid.NewGuid();
        LogoutUserHandler handler = new (db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new () { RefreshTokenToRevoke = "does-not-exist" };

        LogoutUserResult result = await handler.Handle(request, userId);

        result.Outcome.Should().Be(LogoutUserOutcome.NotFound);
    }
    

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_Token_Belongs_To_Different_User()
    {
        AppDbContext db = CreateDb();
        Guid userA = Guid.NewGuid();
        Guid userB = Guid.NewGuid();
        string token = "shared-token";

        db.RefreshTokens.Add(CreateRefreshToken(userA, token));
        await db.SaveChangesAsync();

        LogoutUserHandler handler = new (db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new () { RefreshTokenToRevoke = token };

        LogoutUserResult result = await handler.Handle(request, userB);

        result.Outcome.Should().Be(LogoutUserOutcome.NotFound);
    }
    

    [Fact]
    public async Task Handle_Should_Return_Expired_When_Token_Is_Expired()
    {
        AppDbContext db = CreateDb();
        Guid userId = Guid.NewGuid();
        string token = "expired-token";

        db.RefreshTokens.Add(CreateRefreshToken(userId, token, expiresAtUtc: DateTime.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync();

        LogoutUserHandler handler = new (db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new () { RefreshTokenToRevoke = token };

        LogoutUserResult result = await handler.Handle(request, userId);

        result.Outcome.Should().Be(LogoutUserOutcome.Expired);
    }
    
    [Fact]
    public async Task Handle_Should_Return_AlreadyRevoked_When_Token_Already_Revoked()
    {
        AppDbContext db = CreateDb();
        Guid userId = Guid.NewGuid();
        string token = "revoked-token";

        db.RefreshTokens.Add(CreateRefreshToken(
            userId,
            token,
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            revokedAtUtc: DateTime.UtcNow.AddMinutes(-2)));
        await db.SaveChangesAsync();

        LogoutUserHandler handler = new (db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new () { RefreshTokenToRevoke = token };

        LogoutUserResult result = await handler.Handle(request, userId);

        result.Outcome.Should().Be(LogoutUserOutcome.AlreadyRevoked);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Should_Return_InvalidInput_On_Empty_Token(string badToken)
    {
        AppDbContext db = CreateDb();
        Guid userId = Guid.NewGuid();
        LogoutUserHandler handler = new (db, NullLogger<LogoutUserHandler>.Instance);
        LogoutUserRequest request = new () { RefreshTokenToRevoke = badToken };

        LogoutUserResult result = await handler.Handle(request, userId);

        result.Outcome.Should().Be(LogoutUserOutcome.BadRequest);
        result.Message.Should().Contain("cannot be empty", Exactly.Once());
    }
}