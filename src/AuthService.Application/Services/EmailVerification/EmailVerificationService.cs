using AuthService.Application.Authentication.VerifyEmail;
using AuthService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Application.Services.EmailVerification;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly IEmailService _emailService;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly EmailVerificationSettings _emailVerificationSettings;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EmailVerificationService> _logger;
    
    private const string TokenContext = "EmailVerification";
    
    public EmailVerificationService(
        IEmailService emailService,
        IVerificationTokenService verificationTokenService,
        IOptions<EmailVerificationSettings> emailVerificationOptions,
        AppDbContext dbContext,
        ILogger<EmailVerificationService> logger)
    {
        _emailService = emailService;
        _verificationTokenService = verificationTokenService;
        _emailVerificationSettings = emailVerificationOptions.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task CreateAndSendEmailVerification(User user, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating emailVerification token for {email}", user.Email);
        string verificationToken = await CreateEmailVerification(user.Email, user.Id, cancellationToken);
        _logger.LogDebug("Sending email verification to {email}", user.Email);
        await _emailService.SendVerificationEmailAsync(user.Email, verificationToken);
        _logger.LogDebug("Successfully sent email verification to {email}", user.Email);
    }

    private async Task<string> CreateEmailVerification(string email, Guid userId, CancellationToken cancellationToken)
    {
        // clear DB of this user's previous verification tokens
        await CleanExistingUserTokens(userId, cancellationToken);
        
        string verificationToken = _verificationTokenService.GenerateVerificationToken();
        
        AuthService.Domain.Entities.EmailVerification newEmailVerification = new ()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HashedToken = _verificationTokenService.HashToken(verificationToken, TokenContext),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(_emailVerificationSettings.HoursToLive),
        };
        
        await _dbContext.EmailVerifications.AddAsync(newEmailVerification, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return verificationToken;
    }

    public async Task<(VerifyEmailOutcome outcome, User? user)> VerifyEmail(string token, CancellationToken cancellationToken)
    {
        string hashedToken = _verificationTokenService.HashToken(token, TokenContext);
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        AuthService.Domain.Entities.EmailVerification? emailVerification = await _dbContext.EmailVerifications
            .SingleOrDefaultAsync(t => t.HashedToken == hashedToken, cancellationToken);
        
        if (emailVerification is null)
        {
            _logger.LogDebug("No emailVerification entry found for {token}", token);
            return (VerifyEmailOutcome.LinkNotFound, null);
        }
        
        // We are processing this emailVerification, so let's go ahead and delete all existing tokens for this transaction:
        await CleanExistingUserTokens(emailVerification.UserId, cancellationToken);

        if (emailVerification.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogDebug("Existing emailVerification entry expired for {token}", token);
            await transaction.CommitAsync(cancellationToken);
            return (VerifyEmailOutcome.LinkExpired, null);
        }
        
        // we want to make sure we're only accepting the token for users that haven't already verified their email
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == emailVerification.UserId && u.EmailVerified == false)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.EmailVerified, true)
                .SetProperty(u => u.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);
        
        // if we actually updated a row, updatedCount will be > 0 and return true
        if (updatedCount == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return (VerifyEmailOutcome.LinkInvalid, null);
        }
        
        User user = await _dbContext.Users
            .FirstAsync(u => u.Id == emailVerification.UserId, cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);
        
        return (VerifyEmailOutcome.Success, user);
    }

    private async Task CleanExistingUserTokens(Guid userId, CancellationToken cancellationToken)
    {
        await _dbContext.EmailVerifications.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        _logger.LogDebug("Successfully clean up old emailVerification entries for user {userId}", userId);
    }
}
