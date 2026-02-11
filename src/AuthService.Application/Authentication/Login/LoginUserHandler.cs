using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Extensions;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.Login;

public class LoginUserHandler
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordService _passwordService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<LoginUserHandler> _logger;
    private readonly LoginUserSettings _loginUserSettings;
    
    public LoginUserHandler(
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IPasswordService passwordService,
        IEmailVerificationService emailVerificationService,
        ILogger<LoginUserHandler> logger,
        IOptions<LoginUserSettings> loginUserOptions)
    {
        _db = db;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _passwordService = passwordService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
        _loginUserSettings = loginUserOptions.Value;
    }
    public async Task<LoginUserResult> Handle(LoginUserRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => LoginUserAsync(request, cancellationToken),
            _loginUserSettings.MinimumDurationMs
        );
    }

    private async Task<LoginUserResult> LoginUserAsync(LoginUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogInformation("login invalid input (empty email or password)");
            return new LoginUserResult(LoginUserOutcome.BadRequest, message: "email and password are required");
        }
        
        User? user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        // No user, no valid login
        if (user == null)
        {
            _logger.LogInformation("login username not found: {email}", request.Email);
            return new LoginUserResult(LoginUserOutcome.UsernameNotFound);
        }
        
        bool validPassword = await _passwordService.Verify(
            request.Password,
            user.PasswordSalt,
            user.PasswordHash);

        if (!validPassword)
        {
            _logger.LogInformation("login failed: invalid password for {email}", request.Email);
            return new LoginUserResult(LoginUserOutcome.InvalidPassword);
        }

        if (!user.EmailVerified)
        {
            await _emailVerificationService.CreateAndSendEmailVerification(user, cancellationToken);
            
            // User hasn't verified email yet. we can't authenticate them until this happens.
            return new LoginUserResult(LoginUserOutcome.EmailNotVerified);
        }
        
        ApiToken accessToken = _jwtService.GenerateToken(user.Id, user.Email);
        ApiToken refreshToken = await _refreshTokenService.GenerateRefreshToken(
            user,
            purgeOldTokens: true,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(accessToken.Token) || string.IsNullOrWhiteSpace(refreshToken.Token))
        {
            _logger.LogError("Token generation returned empty tokens for user {UserId}. accessToken: {accesstoken}, refreshToken: {refreshToken}", user.Id, accessToken.Token.RedactToken(),  refreshToken.Token.RedactToken());
            return new LoginUserResult(LoginUserOutcome.ServerError);
        }
        
        return new LoginUserResult(
            LoginUserOutcome.Success,
            accessToken,
            refreshToken);
    }
}