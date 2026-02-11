using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AuthService.Application.Authentication.Register;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;

namespace AuthService.UnitTests.AuthService.Application.Authentication;

public class RegisterUserHandlerTests
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<RegisterUserHandler>> _loggerMock;
    private readonly RegisterUserSettings _registerSettings;
    private readonly IPasswordService _passwordService;
    private readonly Mock<IEmailVerificationService> _emailVerificationService = new ();
    
    public RegisterUserHandlerTests()
    {
        DbContextOptions<AppDbContext> dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(dbOptions);
        _loggerMock = new Mock<ILogger<RegisterUserHandler>>();

        _registerSettings = new RegisterUserSettings
        {
            MinimumDurationMs = 0,
            MinimumPasswordLength = 6
        };

        PasswordSettings passwordSettings = new PasswordSettings
        {
            DegreeOfParallelism = 2,
            MemorySizeKb = 65536,
            Iterations = 2,
            HashLength = 32,
            SaltLength = 16
        };

        _passwordService = new PasswordService(Options.Create(passwordSettings));
    }
    
    [Fact]
    public async Task Handle_Should_Create_User_For_Valid_Requests()
    {
        // Arrange
        RegisterUserRequest request = new()
            { Username = "validuser1", Password = "strongpass123", Email = "user1@example.com" };
        
        RegisterUserHandler handler = new (
            _db,
            _loggerMock.Object,
            _passwordService,
            Options.Create(_registerSettings),
            _emailVerificationService.Object);

        // Act
        RegisterUserResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RegisterUserOutcome.Success);
        result.Message.Should().BeNull();

        User? userInDb = await _db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
        userInDb.Should().NotBeNull();
        userInDb.Username.Should().Be(request.Username);
        userInDb.Email.Should().Be(request.Email);
    }
    
    [Fact]
    public async Task Handle_Should_Return_EmailAlreadyExists_When_Username_Already_Exists()
    {
        // Arrange
        string email = "dupeemail@email.com";
        
        // Seed a user into the database
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

        RegisterUserRequest request = new ()
        {
            Password = "anotherpass",
            Email = email
        };

        RegisterUserHandler handler = new (
            _db,
            _loggerMock.Object,
            _passwordService,
            Options.Create(_registerSettings),
            _emailVerificationService.Object);

        // Act
        RegisterUserResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RegisterUserOutcome.EmailAlreadyExists);
        result.Message.Should().BeNull();
    }
    
    [Fact]
    public async Task Handle_Should_Return_EmailAlreadyExists_When_Username_Already_Exists_But_Not_Verified()
    {
        // Arrange
        string email = "dupeemail@email.com";
        
        // Seed a user into the database
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

        RegisterUserRequest request = new ()
        {
            Password = "anotherpass",
            Email = email
        };

        RegisterUserHandler handler = new (
            _db,
            _loggerMock.Object,
            _passwordService,
            Options.Create(_registerSettings),
            _emailVerificationService.Object);

        // Act
        RegisterUserResult result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(RegisterUserOutcome.EmailExistsButEmailNotVerified);
        result.Message.Should().BeNull();
        _emailVerificationService.Verify(
            x => x.CreateAndSendEmailVerification(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Handle_Should_Return_InvalidInput_For_Invalid_Requests(RegisterUserRequest request, string reason, string expectedMessage)
    {
        // Arrange
        RegisterUserHandler handler = new (
            _db,
            _loggerMock.Object,
            _passwordService,
            Options.Create(_registerSettings),
            _emailVerificationService.Object);

        // Act
        RegisterUserResult result = await handler.Handle(request);

        // Assert
        result.Outcome.Should().Be(RegisterUserOutcome.BadRequest, because: reason);
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain(expectedMessage);
    }

    public static IEnumerable<object[]> InvalidRequests => new List<object[]>
    {
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "" },
            "Email is empty",
            "Email is required"
        },
        new object[]
        {
            new RegisterUserRequest { Username = "validuser", Password = "", Email = "x@x.com" },
            "the password is empty",
            "Password is required"
        },
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "x@x" },
            "Email has no domain",
            "Invalid email address."
        },
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "x" },
            "Email is single character",
            "Invalid email address."
        },
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "x@" },
            "Email has no website",
            "Invalid email address."
        },
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "@x.com" },
            "Email has no identifier",
            "Invalid email address."
        },
        new object[]
        {
            new RegisterUserRequest { Password = "valid123", Email = "ryanx.com" },
            "Email has no @",
            "Invalid email address."
        },
        new object[]
        {
            new RegisterUserRequest { Username = "validuser", Password = "a", Email = "x@x.com" },
            "the password is shorter than the minimum secure length",
            "Password must be at least 6"
        }
    };
}