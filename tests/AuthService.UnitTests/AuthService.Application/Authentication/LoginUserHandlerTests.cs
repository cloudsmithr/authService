using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.Login;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class LoginUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<LoginUserHandler>> _logger;
    private readonly LoginUserSettings _loginUserSettings;
    private readonly IPasswordService _passwordService;
    private readonly Mock<IEmailVerificationService> _mockEmailVerificationService;
    private readonly IRefreshTokenService _refreshTokenService;
    
    
    public LoginUserHandlerTests()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        DbContextOptions<AppDbContext> dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        _db = new AppDbContext(dbOptions);
        _db.Database.EnsureCreated(); 
        
        _mockEmailVerificationService = new Mock<IEmailVerificationService>();
        _logger = new Mock<ILogger<LoginUserHandler>>();
        ILogger<RefreshTokenService> refreshTokenLogger = new Mock<ILogger<RefreshTokenService>>().Object;
        _loginUserSettings = new()
        {
            MinimumDurationMs = 0
        };

        PasswordSettings passwordSettings = new()
        {
            DegreeOfParallelism = 2,
            MemorySizeKb = 65536,
            Iterations = 2,
            HashLength = 32,
            SaltLength = 16
        };

        RefreshTokenSettings refreshTokenSettings = new()
        {
            RefreshTokenLength = 64,
            RefreshTokenLifeTimeInHours = 24,
            RefreshTokenPurgeCutoffInDays = 7
        };

        _passwordService = new PasswordService(Options.Create(passwordSettings));
        _refreshTokenService = new RefreshTokenService(_db, Options.Create(refreshTokenSettings), refreshTokenLogger);
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
    
    private async Task<(User user, string password)> SeedUserAsync(
        string email,
        string plainPassword,
        bool isVerified = true)
    {
        (string hashBase64, string saltBase64) = await _passwordService.HashPassword(plainPassword);

        User user = new ()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordSalt = saltBase64,
            PasswordHash = hashBase64,
            EmailVerified = isVerified,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (user, plainPassword);
    }
    
    // ---------- invalid input variations ----------
    public static IEnumerable<object[]> InvalidRequests => new List<object[]>
    {
        new object[] { new LoginUserRequest { Email = "",      Password = "pw" }, "username is empty"},
        new object[] { new LoginUserRequest { Email = "user",  Password = ""   }, "password is empty"},
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_InvalidInput_For_Invalid_Body(LoginUserRequest request, string reason)
    {
        JwtService jwtService = CreateJwtService();

        LoginUserHandler handler = new (
            _db,
            jwtService,
            _refreshTokenService,
            _passwordService,
            _mockEmailVerificationService.Object,
            _logger.Object,
            Options.Create(_loginUserSettings));

        LoginUserResult result = await handler.Handle(request);

        result.Outcome.Should().Be(LoginUserOutcome.BadRequest, reason);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }
    
    // ---------- user not found ----------
    [Fact]
    public async Task Handle_Should_Return_UsernameNotFound_When_User_Does_Not_Exist()
    {
        JwtService jwtService = CreateJwtService();

        LoginUserHandler handler = new (
            _db,
            jwtService,
            _refreshTokenService,
            _passwordService,
            _mockEmailVerificationService.Object,
            _logger.Object,
            Options.Create(_loginUserSettings));

        LoginUserRequest request = new ()
        {
            Email = "doesnotexist@nope.com",
            Password = "whatever"
        };

        LoginUserResult result = await handler.Handle(request);

        result.Outcome.Should().Be(LoginUserOutcome.UsernameNotFound);
    }
    

    [Fact]
    public async Task Handle_Should_Return_InvalidPassword_When_Password_Does_Not_Match()
    {
        // arrange
        JwtService jwtService = CreateJwtService();
        LoginUserHandler handler = new (
            _db,
            jwtService,
            _refreshTokenService,
            _passwordService,
            _mockEmailVerificationService.Object,
            _logger.Object,
            Options.Create(_loginUserSettings));

        (User user, string _) = await SeedUserAsync("alice@test.com", "correctpw");

        LoginUserRequest request = new ()
        {
            Email = user.Email,
            Password = "wrongpw"
        };

        // act
        LoginUserResult result = await handler.Handle(request);

        // assert
        result.Outcome.Should().Be(LoginUserOutcome.InvalidPassword);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Success_And_Issue_Tokens_When_Credentials_Are_Valid()
    {
        // arrange
        JwtService jwtService = CreateJwtService();
        LoginUserHandler handler = new (
            _db,
            jwtService,
            _refreshTokenService,
            _passwordService,
            _mockEmailVerificationService.Object,
            _logger.Object,
            Options.Create(_loginUserSettings));

        (User user, string password) = await SeedUserAsync("bob@test.com", "supersecret");

        LoginUserRequest request = new ()
        {
            Email = user.Email,
            Password = password
        };

        // act
        LoginUserResult result = await handler.Handle(request);

        // assert: outcome + tokens returned
        result.Outcome.Should().Be(LoginUserOutcome.Success);
        result.AccessToken.Should().NotBeNull();
        result.RefreshToken.Should().NotBeNull();
        result.AccessToken.Token.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Token.Should().NotBeNullOrWhiteSpace();

        // assert: refresh token persisted for this user
        RefreshToken? stored = await _db.RefreshTokens.FirstOrDefaultAsync(rt =>
            rt.UserId == user.Id && rt.Token == result.RefreshToken.Token);

        stored.Should().NotBeNull();
        stored.RevokedAtUtc.Should().BeNull();
        stored.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }
    
    [Fact]
    public async Task Handle_Valid_But_Unverified_Credentials()
    {
        // arrange
        JwtService jwtService = CreateJwtService();
        LoginUserHandler handler = new (
            _db,
            jwtService,
            _refreshTokenService,
            _passwordService,
            _mockEmailVerificationService.Object,
            _logger.Object,
            Options.Create(_loginUserSettings));

        (User user, string password) = await SeedUserAsync("bob@test.com", "supersecret", false);

        LoginUserRequest request = new ()
        {
            Email = user.Email,
            Password = password
        };

        // act
        LoginUserResult result = await handler.Handle(request);

        // assert: outcome + tokens returned
        result.Outcome.Should().Be(LoginUserOutcome.EmailNotVerified);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();

        // assert: refresh token persisted for this user
        RefreshToken? stored = await _db.RefreshTokens.FirstOrDefaultAsync(rt =>
            rt.UserId == user.Id);

        stored.Should().BeNull();
        _mockEmailVerificationService.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }    
}