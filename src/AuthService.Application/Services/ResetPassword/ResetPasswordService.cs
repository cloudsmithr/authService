using AuthService.Application.Authentication.ResetPassword;
using AuthService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Extensions;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Application.Services.ResetPassword;

public class ResetPasswordService : IResetPasswordService
{
    private readonly IEmailService _emailService;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordService _passwordService;
    private readonly ResetPasswordServiceSettings _resetPasswordServiceSettings;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ResetPasswordService> _logger;
    
    private const string TokenContext = "PasswordReset";
    
    public ResetPasswordService(
        IEmailService emailService,
        IVerificationTokenService verificationTokenService,
        IRefreshTokenService refreshTokenService,
        IPasswordService passwordService,
        IOptions<ResetPasswordServiceSettings> resetPasswordSettings,
        AppDbContext dbContext,
        ILogger<ResetPasswordService> logger)
    {
        _emailService = emailService;
        _verificationTokenService = verificationTokenService;
        _refreshTokenService = refreshTokenService;
        _passwordService = passwordService;
        _resetPasswordServiceSettings = resetPasswordSettings.Value;
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async Task CreateAndSendResetPasswordEmail(User user, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating password reset token for {email}", user.Email);
        string verificationToken = await CreatePasswordResetVerification(user.Email, user.Id, cancellationToken);
        _logger.LogDebug("Sending email verification to {email}", user.Email);
        await _emailService.SendPasswordResetEmailAsync(user.Email, verificationToken);
        _logger.LogDebug("Successfully sent email verification to {email}", user.Email);
    }
    
    private async Task<string> CreatePasswordResetVerification(string email, Guid userId, CancellationToken cancellationToken)
    {
        // clear DB of this user's previous verification tokens
        await CleanExistingUserTokens(userId, cancellationToken);
        
        string verificationToken = _verificationTokenService.GenerateVerificationToken();
        
        PasswordResetVerification newPasswordResetVerification = new ()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HashedToken = _verificationTokenService.HashToken(verificationToken, TokenContext),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(_resetPasswordServiceSettings.HoursToLive),
        };

        await _dbContext.PasswordResetVerifications.AddAsync(newPasswordResetVerification, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return verificationToken;
    }
    
    public async Task<ResetPasswordOutcome> ResetPassword(string token, string newPassword,
        CancellationToken cancellationToken)
    {
        string hashedToken = _verificationTokenService.HashToken(token, TokenContext);
        
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        PasswordResetVerification? passwordResetVerification = await _dbContext.PasswordResetVerifications
            .SingleOrDefaultAsync(t => t.HashedToken == hashedToken, cancellationToken);

        if (passwordResetVerification is null)
        {
            _logger.LogDebug("No passwordResetVerification entry found for {token}", token.RedactToken());
            return ResetPasswordOutcome.LinkNotFound;
        }
        
        await CleanExistingUserTokens(passwordResetVerification.UserId, cancellationToken);

        if (passwordResetVerification.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogDebug("Existing passwordResetVerification entry expired for {token}", token.RedactToken());
            await transaction.CommitAsync(cancellationToken);
            return ResetPasswordOutcome.LinkExpired;
        }
        
        (string newPasswordHash, string newPasswordSalt) = await _passwordService.HashPassword(newPassword);
        
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == passwordResetVerification.UserId)
            .ExecuteUpdateAsync(s => 
                s.SetProperty(u => u.PasswordHash, newPasswordHash)
                .SetProperty(u => u.PasswordSalt, newPasswordSalt)
                .SetProperty(u => u.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);
        
        // if we actually updated a row, updatedCount will be > 0 and return true
        if (updatedCount == 0)
        {
            return ResetPasswordOutcome.LinkInvalid;
        }
        
        await _refreshTokenService.PurgeAllTokens(passwordResetVerification.UserId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ResetPasswordOutcome.Success;
    }
    
    private async Task CleanExistingUserTokens(Guid userId, CancellationToken cancellationToken)
    {
        await _dbContext.PasswordResetVerifications.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        _logger.LogDebug("Successfully clean up old reset password verification token entries for user {userId}", userId);
    }
}