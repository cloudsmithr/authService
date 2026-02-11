using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Application.Authentication.ResendEmailVerification;

public class ResendEmailVerificationHandler
{
    private readonly AppDbContext _db;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ResendEmailVerificationSettings _resendEmailVerificationSettings;
    private readonly ILogger<ResendEmailVerificationHandler> _logger;

    public ResendEmailVerificationHandler(
        AppDbContext db,
        IEmailVerificationService emailVerificationService,
        IOptions<ResendEmailVerificationSettings> settings,
        ILogger<ResendEmailVerificationHandler> logger)
    {
        _db = db;
        _emailVerificationService = emailVerificationService;
        _resendEmailVerificationSettings = settings.Value;
        _logger = logger;
    }

    public async Task<ResendEmailVerificationResult> Handle(ResendEmailVerificationRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => ResendVerificationEmailAsync(request, cancellationToken),
            _resendEmailVerificationSettings.MinimumDurationMs
        );
    }

    private async Task<ResendEmailVerificationResult> ResendVerificationEmailAsync(ResendEmailVerificationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return new ResendEmailVerificationResult(ResendEmailVerificationOutcome.BadRequest, "request can't be null");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new ResendEmailVerificationResult(ResendEmailVerificationOutcome.BadRequest, "email can't be empty");
        }
        
        User? existingUser = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser is null)
        {
            _logger.LogDebug("User with email {Email} not found.", request.Email);
            return new ResendEmailVerificationResult(ResendEmailVerificationOutcome.EmailNotFound);
        }

        if (existingUser.EmailVerified)
        {
            _logger.LogDebug("User with email {Email} already verified.", request.Email);
            return new ResendEmailVerificationResult(ResendEmailVerificationOutcome.EmailAlreadyVerified);
        }

        try
        {
            // if our user hasn't verified their email, let's send another verification email.
            await _emailVerificationService.CreateAndSendEmailVerification(existingUser, cancellationToken);
            _logger.LogDebug("Successfully sent verification email to user {Email}", request.Email);

            return new ResendEmailVerificationResult(ResendEmailVerificationOutcome.Success);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Resending of verification email cancelled for user {Email}", request.Email);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resending of verification email {Email} failed.", request.Email);
            throw;
        }
    }
}